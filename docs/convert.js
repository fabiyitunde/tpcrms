/**
 * Converts Loan_Tutorial.md → Loan_Tutorial.html, Loan_Tutorial.pdf, Loan_Tutorial.docx
 * Requires Node.js 18+ and Google Chrome installed at the default Windows path.
 *
 * Usage:
 *   cd docs
 *   npm install
 *   npm run convert
 *
 * Output files are written to the docs/ folder alongside this script.
 */

const fs = require('fs');
const path = require('path');
const { marked } = require('marked');
const puppeteer = require('puppeteer-core');
const HTMLtoDOCX = require('html-to-docx');

// ── Config ────────────────────────────────────────────────────────────────────

const INPUT_FILE = path.join(__dirname, 'Loan_Tutorial.md');
const OUTPUT_HTML = path.join(__dirname, 'Loan_Tutorial.html');
const OUTPUT_PDF  = path.join(__dirname, 'Loan_Tutorial.pdf');
const OUTPUT_DOCX = path.join(__dirname, 'Loan_Tutorial.docx');

// Default Chrome path on Windows. Adjust if yours is different.
const CHROME_PATH = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

// ── BOA brand colours ─────────────────────────────────────────────────────────

const BOA_GREEN   = '#1a6b2a';
const BOA_GREEN_L = '#e8f5eb';
const BOA_DARK    = '#0f3d18';

// ── CSS (embedded into HTML, also used for PDF via Puppeteer) ─────────────────

const CSS = `
  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');

  *, *::before, *::after { box-sizing: border-box; }

  body {
    font-family: 'Inter', 'Segoe UI', Arial, sans-serif;
    font-size: 14px;
    line-height: 1.75;
    color: #1e293b;
    background: #fff;
    margin: 0;
    padding: 0;
  }

  .page-wrap {
    max-width: 900px;
    margin: 0 auto;
    padding: 48px 56px;
  }

  /* ── Cover header ── */
  .doc-header {
    border-bottom: 4px solid ${BOA_GREEN};
    margin-bottom: 40px;
    padding-bottom: 24px;
    display: flex;
    align-items: center;
    gap: 20px;
  }
  .doc-header-logo {
    background: ${BOA_GREEN};
    color: white;
    font-size: 22px;
    font-weight: 800;
    padding: 10px 18px;
    border-radius: 6px;
    letter-spacing: 0.04em;
    white-space: nowrap;
  }
  .doc-header-text h1 {
    margin: 0;
    font-size: 22px;
    font-weight: 700;
    color: ${BOA_DARK};
  }
  .doc-header-text p {
    margin: 4px 0 0;
    font-size: 12px;
    color: #64748b;
  }

  /* ── Typography ── */
  h1 { font-size: 26px; font-weight: 700; color: ${BOA_DARK}; margin: 48px 0 8px; border-bottom: 2px solid ${BOA_GREEN}; padding-bottom: 8px; }
  h2 { font-size: 20px; font-weight: 700; color: ${BOA_GREEN}; margin: 36px 0 8px; }
  h3 { font-size: 16px; font-weight: 600; color: #334155; margin: 28px 0 6px; }
  h4 { font-size: 14px; font-weight: 600; color: #475569; margin: 20px 0 4px; }

  p { margin: 0 0 12px; }
  strong { font-weight: 600; }
  em { font-style: italic; }

  a { color: ${BOA_GREEN}; text-decoration: none; }
  a:hover { text-decoration: underline; }

  /* ── Code / formulas ── */
  pre {
    background: #f1f5f9;
    border: 1px solid #cbd5e1;
    border-left: 4px solid ${BOA_GREEN};
    border-radius: 6px;
    padding: 16px 20px;
    overflow-x: auto;
    font-size: 13px;
    line-height: 1.6;
    margin: 16px 0;
  }
  code {
    font-family: 'Consolas', 'Courier New', monospace;
    font-size: 12.5px;
    background: #f1f5f9;
    border: 1px solid #e2e8f0;
    border-radius: 3px;
    padding: 1px 5px;
  }
  pre code {
    background: none;
    border: none;
    padding: 0;
    font-size: 13px;
  }

  /* ── Tables ── */
  table {
    width: 100%;
    border-collapse: collapse;
    margin: 16px 0;
    font-size: 13px;
  }
  th {
    background: ${BOA_GREEN};
    color: white;
    font-weight: 600;
    padding: 10px 14px;
    text-align: left;
    border: 1px solid ${BOA_GREEN};
  }
  td {
    padding: 9px 14px;
    border: 1px solid #e2e8f0;
    vertical-align: top;
  }
  tr:nth-child(even) td { background: ${BOA_GREEN_L}; }
  tr:hover td { background: #dceee0; }

  /* ── Lists ── */
  ul, ol { margin: 8px 0 12px; padding-left: 24px; }
  li { margin-bottom: 4px; }

  /* ── Blockquotes / callouts ── */
  blockquote {
    margin: 16px 0;
    padding: 12px 20px;
    background: ${BOA_GREEN_L};
    border-left: 4px solid ${BOA_GREEN};
    border-radius: 0 6px 6px 0;
    color: #1e3a22;
    font-size: 13px;
  }
  blockquote p { margin: 0; }

  /* ── Horizontal rule ── */
  hr {
    border: none;
    border-top: 1px solid #e2e8f0;
    margin: 32px 0;
  }

  /* ── TOC ── */
  .toc-section { background: ${BOA_GREEN_L}; border: 1px solid #a7c9b0; border-radius: 8px; padding: 20px 28px; margin-bottom: 36px; }
  .toc-section h2 { margin-top: 0; color: ${BOA_DARK}; font-size: 16px; border: none; }

  /* ── Print / PDF ── */
  @media print {
    body { font-size: 12px; }
    .page-wrap { padding: 24px 32px; max-width: 100%; }
    pre { page-break-inside: avoid; }
    table { page-break-inside: avoid; }
    h1, h2 { page-break-after: avoid; }
  }
`;

// ── Build full HTML document ──────────────────────────────────────────────────

function buildHtml(mdContent) {
  // Configure marked options
  marked.setOptions({ gfm: true, breaks: false });

  const body = marked.parse(mdContent);

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>CRMS Loan Processing Tutorial — Bank of Agriculture</title>
  <style>${CSS}</style>
</head>
<body>
  <div class="page-wrap">
    <div class="doc-header">
      <div class="doc-header-logo">BOA</div>
      <div class="doc-header-text">
        <h1>Bank of Agriculture Limited</h1>
        <p>Credit and Risk Management System (CRMS) &mdash; Loan Processing Tutorial &mdash; ${new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })}</p>
      </div>
    </div>
    ${body}
  </div>
</body>
</html>`;
}

// ── Main ──────────────────────────────────────────────────────────────────────

async function main() {
  // Read source markdown
  if (!fs.existsSync(INPUT_FILE)) {
    console.error(`ERROR: Input file not found: ${INPUT_FILE}`);
    process.exit(1);
  }
  const mdContent = fs.readFileSync(INPUT_FILE, 'utf8');
  const html = buildHtml(mdContent);

  // 1. HTML ──────────────────────────────────────────────────────────────────
  fs.writeFileSync(OUTPUT_HTML, html, 'utf8');
  console.log(`✓ HTML  → ${OUTPUT_HTML}`);

  // 2. PDF (via Puppeteer + system Chrome) ────────────────────────────────────
  if (!fs.existsSync(CHROME_PATH)) {
    console.warn(`⚠ Chrome not found at "${CHROME_PATH}" — skipping PDF. Update CHROME_PATH in convert.js.`);
  } else {
    const browser = await puppeteer.launch({
      executablePath: CHROME_PATH,
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox'],
    });
    try {
      const page = await browser.newPage();
      await page.setContent(html, { waitUntil: 'networkidle0' });
      await page.pdf({
        path: OUTPUT_PDF,
        format: 'A4',
        margin: { top: '20mm', right: '18mm', bottom: '20mm', left: '18mm' },
        printBackground: true,
        displayHeaderFooter: true,
        headerTemplate: `<div style="font-size:9px;color:#64748b;width:100%;text-align:center;padding-top:4px;">
          CRMS Loan Processing Tutorial — Bank of Agriculture Limited — CONFIDENTIAL
        </div>`,
        footerTemplate: `<div style="font-size:9px;color:#64748b;width:100%;text-align:center;padding-bottom:4px;">
          Page <span class="pageNumber"></span> of <span class="totalPages"></span>
        </div>`,
      });
      console.log(`✓ PDF   → ${OUTPUT_PDF}`);
    } finally {
      await browser.close();
    }
  }

  // 3. DOCX (via html-to-docx) ───────────────────────────────────────────────
  const docxBuffer = await HTMLtoDOCX(html, null, {
    title: 'CRMS Loan Processing Tutorial',
    subject: 'Bank of Agriculture — Loan Processing Tutorial',
    creator: 'CRMS Platform',
    description: 'Step-by-step guide to Corporate and NAMP loan processes',
    margins: { top: 1080, right: 1080, bottom: 1080, left: 1080 },
    fontSize: 22,           // 11pt (half-points)
    complexScriptsFont: 'Calibri',
    font: 'Calibri',
    table: { row: { cantSplit: true } },
    header: true,
    footer: true,
    pageNumber: true,
  });
  fs.writeFileSync(OUTPUT_DOCX, docxBuffer);
  console.log(`✓ DOCX  → ${OUTPUT_DOCX}`);

  console.log('\nAll done. Files are in the docs/ folder.');
}

main().catch(err => {
  console.error('Conversion failed:', err.message);
  process.exit(1);
});
