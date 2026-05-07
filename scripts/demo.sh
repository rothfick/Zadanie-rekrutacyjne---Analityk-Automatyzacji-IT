#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:5058}"
SCENARIO_PATH="${SCENARIO_PATH:-samples/scenarios/happy-path-visual-defect.json}"
REVIEWER="${REVIEWER:-service.specialist}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

json_get() {
  python3 -c 'import json, sys; data=json.load(sys.stdin); print(data[sys.argv[1]])' "$1"
}

json_event_names() {
  python3 -c 'import json, sys; [print("- " + item["eventName"]) for item in json.load(sys.stdin)]'
}

json_has_event() {
  python3 -c 'import json, sys; events=json.load(sys.stdin); print(any(item.get("eventName") == sys.argv[1] for item in events))' "$1"
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

require_command curl
require_command python3

echo "Metalpol AI Complaint Automation Demo"
echo "API base URL: $BASE_URL"
echo

echo "Health check:"
curl -sS --fail-with-body "$BASE_URL/health"
echo

echo "1. Sending sample email: $(basename "$SCENARIO_PATH")"
intake_response="$(curl -sS --fail-with-body -X POST "$BASE_URL/api/mock/exchange/messages" \
  -H "Content-Type: application/json" \
  --data @"$SCENARIO_PATH")"

complaint_id="$(printf '%s' "$intake_response" | json_get complaintId)"
status="$(printf '%s' "$intake_response" | json_get status)"
category="$(printf '%s' "$intake_response" | json_get defectCategory)"
confidence="$(printf '%s' "$intake_response" | json_get aiConfidence)"
jira_key="$(printf '%s' "$intake_response" | json_get jiraComplaintKey)"
human_review="$(printf '%s' "$intake_response" | json_get humanReviewRequired)"

timeline_response="$(curl -sS --fail-with-body "$BASE_URL/api/complaints/$complaint_id/timeline")"
order_verified="$(printf '%s' "$timeline_response" | json_has_event OrderVerified)"
batch_verified="$(printf '%s' "$timeline_response" | json_has_event BatchVerified)"

echo "Created complaint: $complaint_id"
echo "Status: $status"
echo "Jira Complaint: $jira_key"
echo "AI category: $category"
echo "AI confidence: $confidence"
echo "Human review required: $human_review"
echo "SAP order verified: $order_verified"
echo "Batch verified: $batch_verified"

echo
echo "2. Timeline:"
printf '%s' "$timeline_response" | json_event_names

echo
echo "3. Approving complaint as confirmed defect..."
approval_payload="$(cat <<JSON
{
  "reviewer": "$REVIEWER",
  "decision": "ConfirmDefect",
  "notes": "Defect confirmed during local demo review."
}
JSON
)"
approval_response="$(curl -sS --fail-with-body -X POST "$BASE_URL/api/complaints/$complaint_id/review/approve" \
  -H "Content-Type: application/json" \
  -d "$approval_payload")"
approval_status="$(printf '%s' "$approval_response" | json_get status)"
correction_key="$(printf '%s' "$approval_response" | json_get correctionIssueKey)"

echo "Status after approval: $approval_status"
echo "Correction ticket created: $correction_key"
echo

kpis_response="$(curl -sS --fail-with-body "$BASE_URL/api/dashboard/kpis")"
total_complaints="$(printf '%s' "$kpis_response" | json_get totalComplaints)"
manual_reviews="$(printf '%s' "$kpis_response" | json_get humanReviewRequired)"
corrections_created="$(printf '%s' "$kpis_response" | json_get correctionsCreated)"
jira_success="$(printf '%s' "$kpis_response" | json_get jiraIssueCreationSuccessRatePercent)"
sap_failure="$(printf '%s' "$kpis_response" | json_get sapVerificationFailureRatePercent)"

echo "4. KPI snapshot:"
echo "Total complaints: $total_complaints"
echo "Human review required: $manual_reviews"
echo "Corrections created: $corrections_created"
echo "Average first response draft time: mocked"
echo "Jira creation success rate: ${jira_success}%"
echo "SAP verification failure rate: ${sap_failure}%"

echo
cat <<'TEXT'
Result summary:
- The API receives a mock Microsoft 365 / Exchange message and creates a complaint record.
- Mock AI extracts order, batch, defect category and draft response deterministically.
- Mock SAP ERP and Jira Cloud integrations keep the demo provider-neutral and repeatable.
- Human approval creates a Correction ticket; AI does not make the final decision.
- KPI output shows how the process becomes measurable.
TEXT
