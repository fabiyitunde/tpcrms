/**
 * Builds the Billing user guide for Freewave's Billing Managers:
 *
 *   Billing_User_Guide.md   (the source: edit this)
 *     -> Billing_User_Guide.pdf
 *     -> Billing_User_Guide.docx
 *
 * Usage, from the docs folder:
 *   npm install            (once)
 *   node build-billing-guide.js
 *
 * The PDF is printed by Google Chrome. The Word file is made by Microsoft Word from the same
 * page, so the two look alike; without Word installed it falls back to the html-to-docx package.
 * Bump VERSION and DATE below whenever the guide changes.
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
const { marked } = require('marked');
const puppeteer = require('puppeteer-core');

const VERSION = '1.0';
const DATE = '10 November 2026';
const TITLE = 'CRMS Billing';
const SUBTITLE = 'User Guide for Billing Managers';

const NAME = 'Billing_User_Guide';
const INPUT = path.join(__dirname, `${NAME}.md`);
const OUT_PDF = path.join(__dirname, `${NAME}.pdf`);
const OUT_DOCX = path.join(__dirname, `${NAME}.docx`);
const LOGO = path.join(__dirname, 'boa_logo.png');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';

const GREEN = '#1a6b2a';
const GREEN_LIGHT = '#e8f5eb';
const GREEN_DARK = '#0f3d18';

// ── Content ──────────────────────────────────────────────────────────────────────────────────────

function slug(text) {
  return text.toLowerCase().replace(/<[^>]+>/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}

/** Markdown to HTML, with an id on every heading and the list of top-level headings for the contents page. */
function render(md) {
  const contents = [];
  const renderer = new marked.Renderer();
  renderer.heading = (text, level, raw) => {
    const id = slug(raw);
    if (level === 1) contents.push({ id, text });
    // The first section and each appendix start a new page; the rest follow on.
    const fresh = level === 1 && (contents.length === 1 || /^Appendix/.test(raw));
    return `<h${level} id="${id}"${fresh ? ' class="fresh"' : ''}>${text}</h${level}>\n`;
  };
  marked.setOptions({ gfm: true, breaks: false });
  return { body: marked.parse(md, { renderer }), contents };
}

const css = `
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', Calibri, Arial, sans-serif; font-size: 10.5pt; line-height: 1.55; color: #1e293b; margin: 0; }

  .cover { height: 245mm; display: flex; flex-direction: column; justify-content: center; page-break-after: always; }
  .cover img { width: 62mm; margin-bottom: 18mm; }
  .cover .rule { width: 28mm; height: 1.6mm; background: ${GREEN}; margin-bottom: 9mm; }
  .cover h1.title { font-size: 30pt; line-height: 1.15; color: ${GREEN_DARK}; margin: 0; border: 0; padding: 0; page-break-before: avoid; }
  .cover .subtitle { font-size: 19pt; color: ${GREEN}; margin: 3mm 0 16mm; font-weight: 600; }
  .cover .lead { font-size: 12pt; color: #475569; max-width: 135mm; margin: 0 0 22mm; }
  .cover table.meta { width: auto; border-collapse: collapse; font-size: 10pt; margin: 0; }
  .cover table.meta td { border: 0; padding: 1.2mm 8mm 1.2mm 0; background: none; color: #334155; }
  .cover table.meta td.k { color: #64748b; }

  .cover p.spacer { display: none; }
  .contents { page-break-after: always; }
  .contents h1 { margin-top: 0; page-break-before: always; }
  .contents p { padding: 2.2mm 0; margin: 0; border-bottom: 1px solid #e2e8f0; font-size: 11pt; }
  .contents a { color: #1e293b; text-decoration: none; }

  h1 { font-size: 19pt; color: ${GREEN_DARK}; margin: 13mm 0 5mm; padding-bottom: 2.5mm; border-bottom: 2px solid ${GREEN}; page-break-after: avoid; }
  h1.fresh { page-break-before: always; margin-top: 0; }
  h2 { font-size: 13.5pt; color: ${GREEN}; margin: 8mm 0 2.5mm; page-break-after: avoid; }
  h3 { font-size: 11.5pt; color: #334155; margin: 6mm 0 2mm; page-break-after: avoid; }
  p { margin: 0 0 3mm; }
  ul, ol { margin: 1.5mm 0 3.5mm; padding-left: 6.5mm; }
  li { margin-bottom: 1.2mm; }
  strong { font-weight: 600; color: #0f172a; }
  a { color: ${GREEN}; }

  code { font-family: Consolas, 'Courier New', monospace; font-size: 9pt; background: #f1f5f9; border-radius: 2px; padding: 0.2mm 1mm; }
  pre { background: #f6f8fa; border: 1px solid #d8dee4; border-left: 3px solid ${GREEN}; border-radius: 3px; padding: 3mm 4mm; margin: 2.5mm 0 4.5mm; white-space: pre-wrap; word-break: break-word; page-break-inside: avoid; }
  pre code { background: none; padding: 0; font-size: 8.4pt; line-height: 1.45; }
  pre.long { page-break-inside: auto; }
  pre.long code { font-size: 7.7pt; line-height: 1.4; }

  table { width: 100%; border-collapse: collapse; margin: 2.5mm 0 5mm; font-size: 9.4pt; }
  th { background: ${GREEN}; color: #fff; font-weight: 600; text-align: left; padding: 2mm 2.8mm; border: 1px solid ${GREEN}; }
  td { padding: 1.8mm 2.8mm; border: 1px solid #d8dee4; vertical-align: top; }
  tr:nth-child(even) td { background: ${GREEN_LIGHT}; }
  tr { page-break-inside: avoid; }
  table.keep { page-break-inside: avoid; }
  p.leadin { page-break-after: avoid; break-after: avoid; }
  td code, th code { font-size: 8.6pt; white-space: nowrap; }
  th code { background: none; color: #fff; padding: 0; }
  td:last-child code { white-space: normal; word-break: break-word; }
`;

function page(body, contents, logoSrc) {
  const toc = contents.map(c => `<p><a href="#${c.id}">${c.text}</a></p>`).join('\n');
  // Code blocks too long for one page must be allowed to break.
  body = body.replace(/<pre><code([^>]*)>([\s\S]*?)<\/code><\/pre>/g, (all, attrs, code) =>
    code.split('\n').length > 24 ? `<pre class="long"><code${attrs}>${code}</code></pre>` : all);

  // Column headings are plain text: a code-styled word on the green heading row is unreadable in Word.
  body = body.replace(/<th>([\s\S]*?)<\/th>/g, cell => cell.replace(/<\/?code>/g, ''));
  // A short table stays on one page, and a line that introduces a table or a code block stays with it.
  body = body.replace(/<table>([\s\S]*?)<\/table>/g, (all, inner) =>
    (inner.match(/<tr>/g) || []).length <= 9 ? `<table class="keep">${inner}</table>` : all);
  body = body.replace(/<p>((?:(?!<\/p>)[\s\S])*:)<\/p>(\s*<(?:table|pre))/g, '<p class="leadin">$1</p>$2');

  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>${TITLE}: ${SUBTITLE}</title>
<style>${css}</style>
</head>
<body>
<div class="cover">
  <p class="spacer">&nbsp;</p>
  <img src="${logoSrc}" alt="Bank of Agriculture">
  <div class="rule"></div>
  <h1 class="title">${TITLE}</h1>
  <div class="subtitle">${SUBTITLE}</div>
  <p class="lead">For the Billing Managers at Freewave Consult Ltd: how the CRMS records what the Bank of Agriculture is charged for, how to set prices and platform fees, close a month into a statement, and act on the alerts.</p>
  <table class="meta">
    <tr><td class="k">Version</td><td>${VERSION}</td></tr>
    <tr><td class="k">Date</td><td>${DATE}</td></tr>
    <tr><td class="k">Audience</td><td>Billing Managers, Freewave Consult Ltd</td></tr>
  </table>
</div>
<div class="contents">
  <h1>Contents</h1>
${toc}
</div>
${body}
</body>
</html>`;
}

// ── PDF, printed by Chrome ───────────────────────────────────────────────────────────────────────

async function writePdf(html) {
  const browser = await puppeteer.launch({ executablePath: CHROME, headless: true, args: ['--no-sandbox'] });
  try {
    const tab = await browser.newPage();
    await tab.setContent(html, { waitUntil: 'load' });
    const strip = 'font-family:Segoe UI,Arial,sans-serif;font-size:7.5pt;color:#64748b;width:100%;padding:0 18mm;display:flex;justify-content:space-between;';
    await tab.pdf({
      path: OUT_PDF,
      format: 'A4',
      margin: { top: '22mm', right: '18mm', bottom: '20mm', left: '18mm' },
      printBackground: true,
      displayHeaderFooter: true,
      headerTemplate: `<div style="${strip}"><span>Bank of Agriculture</span><span>${TITLE}: ${SUBTITLE}</span></div>`,
      footerTemplate: `<div style="${strip}"><span>Version ${VERSION}, ${DATE}</span><span>Page <span class="pageNumber"></span> of <span class="totalPages"></span></span></div>`,
    });
  } finally {
    await browser.close();
  }
}

// ── Word, made by Word itself from the same page ─────────────────────────────────────────────────

const wordScript = `
param([string]$Html, [string]$Docx, [string]$Header, [string]$Footer)
$ErrorActionPreference = 'Stop'
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
  $doc = $word.Documents.Open($Html, $false, $true)
  $word.ActiveWindow.View.Type = 3

  # A4 with the same margins as the PDF.
  $doc.PageSetup.PaperSize = 7
  $doc.PageSetup.TopMargin = 62; $doc.PageSetup.BottomMargin = 57
  $doc.PageSetup.LeftMargin = 51; $doc.PageSetup.RightMargin = 51

  # Pictures arrive as links to files; keep them inside the document.
  foreach ($shape in $doc.InlineShapes) {
    if ($shape.LinkFormat -ne $null) {
      $shape.LinkFormat.SavePictureWithDocument = $true
      $shape.LinkFormat.BreakLink()
    }
  }

  # Tables: full width, heading row repeated on each page, rows kept whole. The first table is
  # the small one on the cover and is left as it is.
  $n = 0
  foreach ($table in $doc.Tables) {
    $n++
    if ($n -eq 1) { continue }
    # Stretch to the text width, keeping the columns in the proportions Word chose.
    $usable = $doc.PageSetup.PageWidth - $doc.PageSetup.LeftMargin - $doc.PageSetup.RightMargin
    $table.AllowAutoFit = $false
    $total = 0
    foreach ($column in $table.Columns) { $total += $column.Width }
    if ($total -gt 0 -and $total -lt $usable - 2) {
      $factor = $usable / $total
      foreach ($column in $table.Columns) { $column.Width = $column.Width * $factor }
    }
    $table.Rows.AllowBreakAcrossPages = 0
    try { $table.Rows.Item(1).HeadingFormat = -1 } catch { }
  }

  # Code blocks arrive one line per paragraph, spaced apart; close them up.
  foreach ($paragraph in $doc.Paragraphs) {
    if ($paragraph.Range.Font.Name -eq 'Consolas' -and $paragraph.Range.Tables.Count -eq 0) {
      $paragraph.Format.SpaceBefore = 0
      $paragraph.Format.SpaceAfter = 0
      $paragraph.Format.LineSpacingRule = 0
    }
  }

  # Running header and page numbers, but not on the cover.
  $section = $doc.Sections.Item(1)
  $section.PageSetup.DifferentFirstPageHeaderFooter = -1
  $head = $section.Headers.Item(1).Range
  $head.Text = $Header
  $head.Font.Name = 'Segoe UI'; $head.Font.Size = 8; $head.Font.Color = 8421504; $head.ParagraphFormat.Alignment = 2
  $foot = $section.Footers.Item(1).Range
  $foot.Text = $Footer + '    Page '
  $foot.Font.Name = 'Segoe UI'; $foot.Font.Size = 8; $foot.Font.Color = 8421504; $foot.ParagraphFormat.Alignment = 2
  $end = $section.Footers.Item(1).Range
  $end.Collapse(0)
  [void]$end.Fields.Add($end, 33)

  # A page opened from HTML starts in Web Layout; the saved file should open as a document.
  $word.ActiveWindow.View.Type = 3
  $doc.SaveAs2($Docx, 16)
  $doc.Close(0)
} finally {
  $word.Quit()
  [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($word)
}
`;

function writeDocxWithWord(html) {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), 'guide-'));
  const htmlFile = path.join(work, 'guide.html');
  const script = path.join(work, 'to-docx.ps1');
  // Word does not lay out flexbox; give the cover plain spacing instead.
  const forWord = html
    .replace('.cover { height: 245mm; display: flex; flex-direction: column; justify-content: center; page-break-after: always; }',
             '.cover { page-break-after: always; }')
    .replace('.cover p.spacer { display: none; }', '.cover p.spacer { margin: 0 0 38mm; }');
  fs.writeFileSync(htmlFile, '\ufeff' + forWord, 'utf8');
  fs.writeFileSync(script, wordScript, 'utf8');
  try {
    if (fs.existsSync(OUT_DOCX)) fs.unlinkSync(OUT_DOCX);
    execFileSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script,
      '-Html', htmlFile, '-Docx', OUT_DOCX,
      '-Header', `${TITLE}: ${SUBTITLE}`, '-Footer', `Version ${VERSION}, ${DATE}`], { stdio: 'pipe' });
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
}

async function writeDocxFallback(html) {
  const HTMLtoDOCX = require('html-to-docx');
  const buffer = await HTMLtoDOCX(html, null, {
    title: `${TITLE}: ${SUBTITLE}`, creator: 'Bank of Agriculture',
    margins: { top: 1240, right: 1020, bottom: 1140, left: 1020 },
    font: 'Calibri', fontSize: 21, table: { row: { cantSplit: true } }, footer: true, pageNumber: true,
  });
  fs.writeFileSync(OUT_DOCX, buffer);
}

// ── Main ─────────────────────────────────────────────────────────────────────────────────────────

async function main() {
  const { body, contents } = render(fs.readFileSync(INPUT, 'utf8'));

  const logoData = 'data:image/png;base64,' + fs.readFileSync(LOGO).toString('base64');
  await writePdf(page(body, contents, logoData));
  console.log(`PDF   ${OUT_PDF}`);

  const forWord = page(body, contents, 'file:///' + LOGO.replace(/\\/g, '/'));
  try {
    writeDocxWithWord(forWord);
    console.log(`DOCX  ${OUT_DOCX}  (made by Word)`);
  } catch (error) {
    console.warn(`Word could not be used (${String(error.message).split('\n')[0]}); using html-to-docx instead.`);
    await writeDocxFallback(page(body, contents, logoData));
    console.log(`DOCX  ${OUT_DOCX}  (made by html-to-docx)`);
  }
}

main().catch(error => { console.error('Build failed:', error.message); process.exit(1); });
