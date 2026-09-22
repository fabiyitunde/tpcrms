#!/usr/bin/env bash
# Diagnose why the deploy workflow cannot reach the CRMS EC2 box.
#
# Usage: scripts/check-ec2-reachable.sh <host> [ssh-user] [ssh-key]
#   e.g. scripts/check-ec2-reachable.sh 3.249.96.103 ec2-user ~/.ssh/crms-deploy.pem
#
# Tests the same three things the workflow depends on, in the order they fail:
#   22   — SSH, used by scp/ssh in the deploy step
#   8980 — CRMS.API   (proves the box is up even if 22 is blocked)
#   8981 — CRMS.Web.Intranet
#
# IMPORTANT: a pass here proves the host is reachable FROM THIS MACHINE. It does
# not prove GitHub Actions can reach it — runners come from Azure IP ranges that
# rotate constantly. If port 22 is open to your IP but not to 0.0.0.0/0, this
# script passes and the workflow still times out. See the note at the end.

set -uo pipefail

HOST="${1:-}"
SSH_USER="${2:-ec2-user}"
SSH_KEY="${3:-}"

if [ -z "$HOST" ]; then
    echo "usage: $0 <host> [ssh-user] [ssh-key]" >&2
    exit 2
fi

TIMEOUT=8

port_open() {
    timeout "$TIMEOUT" bash -c "exec 3<>/dev/tcp/$1/$2" 2>/dev/null
}

check_port() {
    local port="$1" label="$2"
    printf '  %-6s %-24s ' "$port" "$label"
    if port_open "$HOST" "$port"; then
        echo "OPEN"
        return 0
    else
        echo "no response (timed out after ${TIMEOUT}s)"
        return 1
    fi
}

echo "Checking $HOST"
echo

check_port 22   "SSH (deploy)"          ; ssh_ok=$?
check_port 8980 "CRMS.API"              ; api_ok=$?
check_port 8981 "CRMS.Web.Intranet"     ; web_ok=$?

echo
echo "── Reading ──────────────────────────────────────────────────────────────"

if [ $ssh_ok -ne 0 ] && [ $api_ok -ne 0 ] && [ $web_ok -ne 0 ]; then
    cat <<'MSG'
Nothing responded on any port.

The instance is stopped/terminated, or this address is stale. A stopped EC2
instance gets a NEW public IPv4 when restarted unless an Elastic IP is
attached, so the EC2_HOST secret may simply be pointing at an address that no
longer belongs to you.

  -> EC2 console: is the instance "running"? What is its current public IPv4?
  -> If the IP changed, update the EC2_HOST repository secret, then re-run the
     workflow from the Actions tab (it has workflow_dispatch).
MSG
elif [ $ssh_ok -ne 0 ]; then
    cat <<'MSG'
The box is UP (an app port answered) but port 22 did not.

That isolates it to SSH reachability, not the instance:
  -> Security group: is there an inbound rule for TCP 22, and what is its
     source? If it is scoped to specific IPs, GitHub-hosted runners will not
     match — their addresses come from Azure ranges that change constantly.
  -> Network ACL / route table changes on the subnet.
  -> sshd stopped on the box (less likely: that gives "connection refused",
     not a timeout).
MSG
else
    echo "Port 22 is reachable FROM THIS MACHINE."
    if [ -n "$SSH_KEY" ]; then
        echo
        echo "Trying an actual SSH auth handshake with $SSH_KEY ..."
        ssh -i "$SSH_KEY" \
            -o BatchMode=yes -o StrictHostKeyChecking=no \
            -o ConnectTimeout="$TIMEOUT" \
            "$SSH_USER@$HOST" 'echo "  connected OK as $(whoami) on $(hostname)"' \
            || echo "  SSH auth failed — reachable, but the key or user was rejected."
    else
        echo "(pass an ssh key as the 3rd argument to also test authentication)"
    fi
    cat <<'MSG'

NOTE: this does not prove the workflow can connect. Your machine and a GitHub
runner have different source IPs. If the deploy still times out after this
passes, the security group is almost certainly allowlisting your IP but not
0.0.0.0/0.
MSG
fi

echo
echo "── Durable fix ──────────────────────────────────────────────────────────"
echo "Attach an Elastic IP so the address survives stop/start, otherwise this"
echo "recurs every time the instance cycles. Longer term, SSM Session Manager"
echo "or a self-hosted runner removes the need to expose port 22 at all."
