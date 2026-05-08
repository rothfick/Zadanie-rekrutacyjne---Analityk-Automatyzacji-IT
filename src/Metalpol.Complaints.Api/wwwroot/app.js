const state = {
  scenarios: [],
  selectedScenarioId: "",
  selectedComplaintId: localStorage.getItem("metalpol.selectedComplaintId") || "",
  lastIntakeResponse: null,
  complaint: null,
  timeline: []
};

const els = {
  healthBadge: document.getElementById("healthBadge"),
  selectedComplaintBadge: document.getElementById("selectedComplaintBadge"),
  errorBanner: document.getElementById("errorBanner"),
  dashboardKpis: document.getElementById("dashboardKpis"),
  kpiDetails: document.getElementById("kpiDetails"),
  scenarioSelect: document.getElementById("scenarioSelect"),
  scenarioInfo: document.getElementById("scenarioInfo"),
  processScenarioButton: document.getElementById("processScenarioButton"),
  complaintSummary: document.getElementById("complaintSummary"),
  nextActionHint: document.getElementById("nextActionHint"),
  complaintDetails: document.getElementById("complaintDetails"),
  timeline: document.getElementById("timeline"),
  reviewerInput: document.getElementById("reviewerInput"),
  reviewNotes: document.getElementById("reviewNotes"),
  reviewResult: document.getElementById("reviewResult"),
  refreshKpisButton: document.getElementById("refreshKpisButton"),
  refreshComplaintButton: document.getElementById("refreshComplaintButton"),
  resetDemoButton: document.getElementById("resetDemoButton")
};

async function apiRequest(path, options = {}) {
  const { headers, ...requestOptions } = options;
  const response = await fetch(path, {
    ...requestOptions,
    headers: { Accept: "application/json", ...(headers || {}) }
  });

  if (!response.ok) {
    let details = "";
    try {
      const body = await response.json();
      details = body.error || body.title || JSON.stringify(body);
    } catch {
      details = await response.text();
    }

    const error = new Error(details || `Żądanie zakończone błędem: ${response.status}`);
    error.status = response.status;
    throw error;
  }

  const contentType = response.headers.get("content-type") || "";
  return contentType.includes("application/json") ? response.json() : response.text();
}

function showError(message) {
  els.errorBanner.hidden = false;
  els.errorBanner.textContent = message;
  els.errorBanner.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function clearError() {
  els.errorBanner.hidden = true;
  els.errorBanner.textContent = "";
}

function setButtonBusy(button, busy, textWhenBusy = "Ładowanie...") {
  if (!button) {
    return () => {};
  }

  const previous = button.innerHTML;
  button.disabled = busy;
  if (busy) {
    button.textContent = textWhenBusy;
  }

  return () => {
    button.disabled = false;
    button.innerHTML = previous;
  };
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function display(value, fallback = "-") {
  if (value === null || value === undefined || value === "") {
    return fallback;
  }

  if (Array.isArray(value)) {
    return value.length ? value.join(", ") : fallback;
  }

  if (typeof value === "boolean") {
    return value ? "true" : "false";
  }

  return value;
}

function formatPercent(value) {
  const number = Number(value);
  return Number.isFinite(number) ? `${number.toFixed(number % 1 === 0 ? 0 : 2)}%` : "-";
}

function formatConfidence(value) {
  const number = Number(value);
  return Number.isFinite(number) ? number.toFixed(2) : "-";
}

function statusClass(status) {
  if (!status) {
    return "";
  }

  if (["ResponseDrafted", "CorrectionCreated", "Closed"].includes(status)) {
    return "status-ok";
  }

  if (["HumanReviewRequired", "MissingData", "SapMismatch", "DuplicateLinked"].includes(status)) {
    return "status-warning";
  }

  if (["Failed"].includes(status)) {
    return "status-error";
  }

  return "";
}

function badgeClass(status) {
  if (["ResponseDrafted", "CorrectionCreated", "Closed"].includes(status)) {
    return "badge-ok";
  }

  if (["Failed"].includes(status)) {
    return "badge-error";
  }

  return "badge-soft";
}

async function loadHealth() {
  try {
    const health = await apiRequest("/health", { headers: { Accept: "text/plain" } });
    els.healthBadge.textContent = `API ${health}`;
    els.healthBadge.className = "badge badge-ok";
  } catch (error) {
    els.healthBadge.textContent = "API offline";
    els.healthBadge.className = "badge badge-error";
    showError(`API nie odpowiada. Uruchom: dotnet run --project src/Metalpol.Complaints.Api. Szczegóły: ${error.message}`);
  }
}

async function loadScenarios() {
  try {
    state.scenarios = await apiRequest("/api/demo/scenarios");
    if (!state.scenarios.length) {
      els.scenarioSelect.innerHTML = "<option>Brak scenariuszy</option>";
      els.processScenarioButton.disabled = true;
      return;
    }

    els.scenarioSelect.innerHTML = state.scenarios
      .map(scenario => `<option value="${escapeHtml(scenario.id)}">${escapeHtml(scenario.label)}</option>`)
      .join("");
    state.selectedScenarioId = state.scenarios[0].id;
    renderScenarioInfo();
  } catch (error) {
    showError(`Nie udało się załadować scenariuszy demo. Sprawdź katalog samples/scenarios. Szczegóły: ${error.message}`);
    els.scenarioSelect.innerHTML = "<option>Ładowanie scenariuszy nie powiodło się</option>";
    els.processScenarioButton.disabled = true;
  }
}

function renderScenarioInfo() {
  const scenario = state.scenarios.find(item => item.id === els.scenarioSelect.value) || state.scenarios[0];
  if (!scenario) {
    els.scenarioInfo.textContent = "Brak dostępnych scenariuszy.";
    return;
  }

  state.selectedScenarioId = scenario.id;
  els.scenarioInfo.innerHTML = `
    <strong>${escapeHtml(scenario.label)}</strong>
    <span>${escapeHtml(scenario.description)}</span>
    <p><strong>Cel scenariusza:</strong> ${escapeHtml(scenario.businessCase)}</p>
    <div class="scenario-file">Plik: ${escapeHtml(scenario.fileName)}</div>
  `;
}

async function loadKpis() {
  const done = setButtonBusy(els.refreshKpisButton, true, "Odświeżanie...");
  try {
    const kpis = await apiRequest("/api/dashboard/kpis");
    renderDashboardKpis(kpis);
    renderKpiDetails(kpis);
  } catch (error) {
    showError(`Nie udało się odświeżyć KPI. Sprawdź, czy API działa. Szczegóły: ${error.message}`);
  } finally {
    done();
  }
}

function renderDashboardKpis(kpis) {
  const confidence = kpis.aiExtractionConfidenceDistribution || {};
  const lowConfidence = (confidence["0.00-0.59"] || 0) + (confidence["0.60-0.84"] || 0);
  const cards = [
    ["Reklamacje razem", kpis.totalComplaints ?? 0],
    ["Do review teraz", kpis.humanReviewRequired ?? 0],
    ["Jira Cloud success", formatPercent(kpis.jiraIssueCreationSuccessRatePercent)],
    ["SAP ERP issues", formatPercent(kpis.sapVerificationFailureRatePercent)],
    ["Low confidence", lowConfidence]
  ];

  els.dashboardKpis.innerHTML = cards
    .map(([label, value]) => `
      <article class="kpi-card">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
      </article>
    `)
    .join("");
}

function renderKpiDetails(kpis) {
  const rows = [
    ["Backlog", kpis.backlogSize ?? 0],
    ["Correction tickets", kpis.correctionsCreated ?? 0],
    ["Zamknięte sprawy", kpis.closedComplaints ?? 0],
    ["SLA breaches", kpis.slaBreachCount ?? 0],
    ["Udział spraw do review", formatPercent(kpis.percentRequiringHumanReview)],
    ["Confidence distribution", summarizeMap(kpis.aiExtractionConfidenceDistribution)],
    ["Kategorie wad", summarizeMap(kpis.complaintCountByDefectCategory)],
    ["Linie produkcyjne", summarizeMap(kpis.complaintCountByProductionLine)],
    ["Batch / partia", summarizeMap(kpis.complaintCountByBatch)]
  ];

  els.kpiDetails.className = "metric-list";
  els.kpiDetails.innerHTML = rows
    .map(([label, value]) => `
      <div class="metric-row">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
      </div>
    `)
    .join("");
}

function summarizeMap(value) {
  if (!value || !Object.keys(value).length) {
    return "-";
  }

  return Object.entries(value)
    .map(([key, count]) => `${key}: ${count}`)
    .join(", ");
}

async function processSelectedScenario() {
  const scenarioId = els.scenarioSelect.value;
  if (!scenarioId) {
    showError("Wybierz scenariusz demo.");
    return;
  }

  clearError();
  setReviewEnabled(false);
  const done = setButtonBusy(els.processScenarioButton, true, "Przetwarzanie...");
  try {
    const payload = await apiRequest(`/api/demo/scenarios/${encodeURIComponent(scenarioId)}`);
    const intake = await apiRequest("/api/mock/exchange/messages", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    state.selectedComplaintId = intake.complaintId;
    state.lastIntakeResponse = intake;
    localStorage.setItem("metalpol.selectedComplaintId", state.selectedComplaintId);
    renderIntakeSummary(intake);
    await refreshSelectedComplaint();
    await loadKpis();
    document.getElementById("result")?.scrollIntoView({ behavior: "smooth", block: "start" });
  } catch (error) {
    showError(`Scenariusz nie został przetworzony. Sprawdź, czy API działa i czy scenariusz jest poprawny. Szczegóły: ${error.message}`);
  } finally {
    done();
    setReviewEnabled(true, state.complaint);
  }
}

function renderIntakeSummary(intake) {
  els.complaintSummary.className = "complaint-summary";
  els.complaintSummary.innerHTML = [
    ["Complaint id", intake.complaintId],
    ["Status", intake.status],
    ["Jira Complaint", intake.jiraComplaintKey],
    ["AI category", intake.defectCategory],
    ["AI confidence", formatConfidence(intake.aiConfidence)],
    ["Human review required", display(intake.humanReviewRequired)],
    ["Duplicate", intake.duplicate]
  ].map(([label, value]) => `
    <div class="summary-line">
      <span>${escapeHtml(label)}</span>
      <strong class="${statusClass(value)}">${escapeHtml(display(value))}</strong>
    </div>
  `).join("");
}

async function refreshSelectedComplaint(options = {}) {
  if (!state.selectedComplaintId) {
    renderEmptyComplaint();
    return;
  }

  const done = setButtonBusy(els.refreshComplaintButton, true, "Odświeżanie...");
  try {
    const [complaint, timeline] = await Promise.all([
      apiRequest(`/api/complaints/${encodeURIComponent(state.selectedComplaintId)}`),
      apiRequest(`/api/complaints/${encodeURIComponent(state.selectedComplaintId)}/timeline`)
    ]);

    state.complaint = complaint;
    state.timeline = Array.isArray(timeline) ? timeline : [];
    renderComplaintDetails(complaint);
    renderComplaintSummaryFromDetails(complaint);
    renderTimeline(state.timeline);
    updateSelectedComplaintBadge(complaint);
  } catch (error) {
    if (error.status === 404) {
      clearSelectedComplaint();
      renderEmptyComplaint();
      if (!options.silentMissing) {
        showError("Wybrana reklamacja nie istnieje już w stanie in-memory. Wybierz scenariusz i uruchom demo ponownie.");
      }
      return;
    }

    showError(`Nie udało się odświeżyć reklamacji. Szczegóły: ${error.message}`);
    renderEmptyComplaint();
  } finally {
    done();
  }
}

function renderEmptyComplaint() {
  state.complaint = null;
  state.timeline = [];
  els.complaintSummary.className = "complaint-summary empty-state";
  els.complaintSummary.textContent = "Brak wybranej reklamacji. Wybierz scenariusz i kliknij \"Przetwórz scenariusz\".";
  els.complaintDetails.className = "details-grid empty-state";
  els.complaintDetails.textContent = "Brak wybranej reklamacji. Krok 1: wybierz scenariusz. Krok 2: kliknij \"Przetwórz scenariusz\".";
  els.timeline.className = "timeline empty-state";
  els.timeline.textContent = "Timeline pokaże eventy procesu po intake reklamacji.";
  els.selectedComplaintBadge.textContent = "Brak wybranej reklamacji";
  els.selectedComplaintBadge.className = "badge badge-soft";
  els.nextActionHint.className = "next-action-hint empty-state";
  els.nextActionHint.textContent = "Po przetworzeniu scenariusza pokażemy następny najlepszy krok w demo.";
  els.reviewResult.className = "review-result empty-state";
  els.reviewResult.textContent = "Po przetworzeniu reklamacji kliknij Potwierdź wadę, aby pokazać utworzenie mock Jira Correction.";
  setReviewEnabled(false);
}

function clearSelectedComplaint() {
  state.selectedComplaintId = "";
  state.lastIntakeResponse = null;
  localStorage.removeItem("metalpol.selectedComplaintId");
}

function renderComplaintSummaryFromDetails(complaint) {
  const summaryRows = [
    ["Complaint id", complaint.complaintId],
    ["Status", complaint.status],
    ["Source message", complaint.messageId],
    ["Jira Complaint", complaint.jiraComplaintKey],
    ["Correction", complaint.correctionTicketKey]
  ];
  if (state.lastIntakeResponse?.complaintId === complaint.complaintId) {
    summaryRows.push(["Duplicate", state.lastIntakeResponse.duplicate]);
  }

  els.complaintSummary.className = "complaint-summary";
  els.complaintSummary.innerHTML = summaryRows.map(([label, value]) => `
    <div class="summary-line">
      <span>${escapeHtml(label)}</span>
      <strong class="${statusClass(value)}">${escapeHtml(display(value))}</strong>
    </div>
  `).join("");
  renderNextActionHint(complaint);
}

function renderComplaintDetails(complaint) {
  const sapStatus = inferSapStatus();
  const rows = [
    ["Complaint id", complaint.complaintId],
    ["Status", complaint.status],
    ["Order number", complaint.orderNumber],
    ["Batch number", complaint.batchNumber],
    ["Defect category", complaint.defectCategory],
    ["AI confidence", formatConfidence(complaint.aiConfidence)],
    ["Missing fields", complaint.missingFields],
    ["Customer match", complaint.customerId],
    ["SAP verification", sapStatus],
    ["Jira Complaint key", complaint.jiraComplaintKey],
    ["Correction key", complaint.correctionTicketKey],
    ["Prompt injection", complaint.promptInjectionDetected],
    ["Human review reason", complaint.humanReviewReason],
    ["Attachments", summarizeAttachments(complaint.attachments)],
    ["Traceability", `sourceMessageId: ${display(complaint.messageId)}, receivedAt: ${display(formatDate(complaint.receivedAt))}`, "wide"],
    ["Response draft", complaint.responseDraft, "wide"]
  ];

  els.complaintDetails.className = "details-grid";
  els.complaintDetails.innerHTML = rows
    .map(([label, value, width]) => `
      <div class="detail-item ${width === "wide" ? "wide" : ""}">
        <span>${escapeHtml(label)}</span>
        ${width === "wide" ? `<p>${escapeHtml(display(value))}</p>` : `<strong class="${statusClass(value)}">${escapeHtml(display(value))}</strong>`}
      </div>
    `)
    .join("");
}

function summarizeAttachments(attachments) {
  if (!Array.isArray(attachments) || !attachments.length) {
    return "-";
  }

  return attachments
    .map(attachment => `${attachment.fileName || attachment.attachmentId}${attachment.storageUri ? " archived" : ""}`)
    .join(", ");
}

function inferSapStatus() {
  const events = state.timeline.map(item => item.eventName);
  if (events.includes("SapMismatchDetected")) {
    return "SAP mismatch";
  }

  if (events.includes("OrderVerified") && events.includes("BatchVerified")) {
    return "Order and batch verified";
  }

  if (events.includes("OrderVerified")) {
    return "Order verified";
  }

  if (state.complaint?.status === "SapMismatch") {
    return "SAP mismatch";
  }

  return "Pending or requires review";
}

function renderTimeline(timeline) {
  if (!timeline.length) {
    els.timeline.className = "timeline empty-state";
    els.timeline.textContent = "Brak eventów dla wybranej reklamacji. Timeline pojawia się po przyjęciu maila przez mock Exchange endpoint.";
    return;
  }

  els.timeline.className = "timeline";
  els.timeline.innerHTML = `
    <div class="workflow-board" aria-label="Complaint workflow">
      ${renderWorkflowStages(timeline)}
    </div>
    ${renderAuditLog(timeline)}
  `;
}

function renderWorkflowStages(timeline) {
  const stages = [
    {
      title: "Intake",
      description: "Mail trafia do procesu, a zdjęcia są archiwizowane.",
      events: ["EmailReceived", "AttachmentsStored"]
    },
    {
      title: "AI triage",
      description: "AI wyciąga dane, język, kategorię i confidence.",
      events: ["ComplaintParsed", "DefectClassified"]
    },
    {
      title: "Walidacja biznesowa",
      description: "Klient, order i batch są sprawdzane w systemach źródłowych.",
      events: ["CustomerMatched", "OrderVerified", "BatchVerified", "SapMismatchDetected"]
    },
    {
      title: "Jira i draft",
      description: "Powstaje Jira Complaint oraz draft odpowiedzi dla klienta.",
      events: ["JiraComplaintCreated", "ResponseDrafted"]
    },
    {
      title: "Human review",
      description: "Specjalista zatwierdza, odrzuca albo prosi o dane.",
      events: ["HumanReviewRequested", "HumanReviewCompleted", "CustomerClarificationRequested", "ComplaintClosed"]
    },
    {
      title: "Quality action",
      description: "Po potwierdzeniu wady powstaje Jira Correction.",
      events: ["CorrectionTicketCreated"]
    }
  ];

  const reliabilityStage = {
    title: "Reliability guard",
    description: "System chroni proces przed duplikatami i stanami błędnymi.",
    events: ["DuplicateLinked", "ComplaintFailed"]
  };

  const stageCards = stages.map((stage, index) => renderWorkflowStage(stage, timeline, index + 1));
  if (pickEvents(timeline, reliabilityStage.events).length) {
    stageCards.push(renderWorkflowStage(reliabilityStage, timeline, stages.length + 1, true));
  }

  return stageCards.join("");
}

function renderWorkflowStage(stage, timeline, number, isReliability = false) {
  const matchedEvents = pickEvents(timeline, stage.events);
  const state = matchedEvents.length ? "done" : "pending";
  const eventItems = matchedEvents.length
    ? matchedEvents.map(renderWorkflowEvent).join("")
    : `<span class="workflow-placeholder">Czeka na warunek procesu</span>`;

  return `
    <article class="workflow-stage workflow-${state} ${isReliability ? "workflow-reliability" : ""}">
      <div class="workflow-stage-top">
        <span class="workflow-number">${number}</span>
        <span class="workflow-state">${state === "done" ? "Done" : "Pending"}</span>
      </div>
      <h3>${escapeHtml(stage.title)}</h3>
      <p>${escapeHtml(stage.description)}</p>
      <div class="workflow-events">
        ${eventItems}
      </div>
    </article>
  `;
}

function renderWorkflowEvent(item) {
  return `
    <span class="workflow-event">
      <strong>${escapeHtml(eventDisplayName(item.eventName))}</strong>
      <code>${escapeHtml(item.eventName)}</code>
      <small>${escapeHtml(formatDate(item.occurredAt))}</small>
    </span>
  `;
}

function renderAuditLog(timeline) {
  return `
    <details class="audit-log">
      <summary>Audit log: ${timeline.length} eventów technicznych</summary>
      <div class="audit-list">
        ${timeline.map(renderAuditItem).join("")}
      </div>
    </details>
  `;
}

function renderAuditItem(item) {
  const metadata = item.metadata && Object.keys(item.metadata).length
    ? `<pre class="timeline-metadata">${escapeHtml(JSON.stringify(item.metadata, null, 2))}</pre>`
    : "";

  return `
    <article class="timeline-item">
      <div class="timeline-title">
        <strong>${escapeHtml(item.eventName)}</strong>
        <span class="timeline-time">${escapeHtml(formatDate(item.occurredAt))}</span>
      </div>
      <p class="timeline-description">${escapeHtml(item.description || item.source || "Domain event")}</p>
      ${metadata}
    </article>
  `;
}

function pickEvents(timeline, eventNames) {
  const wanted = new Set(eventNames);
  return timeline.filter(item => wanted.has(item.eventName));
}

function eventDisplayName(eventName) {
  const labels = {
    EmailReceived: "Mail odebrany",
    AttachmentsStored: "Zdjęcia zapisane",
    ComplaintParsed: "Dane wyciągnięte",
    DefectClassified: "Wada sklasyfikowana",
    CustomerMatched: "Klient dopasowany",
    OrderVerified: "Order OK",
    BatchVerified: "Batch OK",
    SapMismatchDetected: "SAP mismatch",
    JiraComplaintCreated: "Complaint w Jira",
    ResponseDrafted: "Draft odpowiedzi",
    HumanReviewRequested: "Review wymagany",
    HumanReviewCompleted: "Review zakończony",
    CustomerClarificationRequested: "Prośba o dane",
    CorrectionTicketCreated: "Correction w Jira",
    ComplaintClosed: "Sprawa zamknięta",
    DuplicateLinked: "Duplikat podpięty",
    ComplaintFailed: "Błąd procesu"
  };

  return labels[eventName] || eventName;
}

function formatDate(value) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
}

function updateSelectedComplaintBadge(complaint) {
  if (!complaint) {
    els.selectedComplaintBadge.textContent = "Brak wybranej reklamacji";
    els.selectedComplaintBadge.className = "badge badge-soft";
    return;
  }

  els.selectedComplaintBadge.textContent = `${complaint.complaintId} / ${complaint.status}`;
  els.selectedComplaintBadge.className = `badge ${badgeClass(complaint.status)}`;
  setReviewEnabled(true, complaint);
}

function canReview(complaint) {
  if (!complaint) {
    return false;
  }

  return ["ResponseDrafted", "HumanReviewRequired", "MissingData", "CustomerResponseApproved"].includes(complaint.status)
    && !complaint.correctionTicketKey;
}

function setReviewEnabled(enabled, complaint = null) {
  const allow = enabled && (!complaint || canReview(complaint));
  document.querySelectorAll("[data-review-decision]").forEach(button => {
    button.disabled = !allow;
  });
}

function renderNextActionHint(complaint) {
  const status = complaint.status;
  let message = "Następny krok: pokaż timeline, żeby połączyć działające MVP z Event Stormingiem.";

  if (status === "ResponseDrafted") {
    message = "Następny krok: sprawdź draft i kliknij Potwierdź wadę, żeby utworzyć Jira Correction po decyzji człowieka.";
  } else if (status === "HumanReviewRequired") {
    message = "Następny krok: sprawdź Human review reason i wybierz decyzję specjalisty. AI nie podejmuje finalnej decyzji.";
  } else if (status === "MissingData") {
    message = "Następny krok: system prosi o uzupełnienie danych zamiast zgadywać order albo batch.";
  } else if (status === "CorrectionCreated") {
    message = "Efekt końcowy: mock Jira Correction został utworzony po review. Human review jest teraz zablokowany celowo. Kliknij Reset demo, jeśli chcesz pokazać świeży przebieg od początku.";
  } else if (status === "Closed") {
    message = "Efekt końcowy: reklamacja została zamknięta po decyzji specjalisty. Kliknij Reset demo, jeśli chcesz pokazać świeży przebieg od początku.";
  } else if (status === "DuplicateLinked") {
    message = "Efekt końcowy: duplikat został podpięty do istniejącej reklamacji bez tworzenia drugiego Jira Complaint.";
  }

  els.nextActionHint.className = "next-action-hint";
  els.nextActionHint.textContent = message;
}

async function submitReview(decision) {
  if (!state.selectedComplaintId) {
    showError("Najpierw wybierz lub przetwórz reklamację.");
    return;
  }

  clearError();
  const button = document.querySelector(`[data-review-decision="${decision}"]`);
  setReviewEnabled(false);
  const done = setButtonBusy(button, true, "Wysyłanie...");
  try {
    const result = await apiRequest(`/api/complaints/${encodeURIComponent(state.selectedComplaintId)}/review/approve`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        reviewer: els.reviewerInput.value || "Service Specialist Demo",
        decision,
        notes: els.reviewNotes.value || "Validated during demo review."
      })
    });

    els.reviewResult.className = "review-result";
    els.reviewResult.innerHTML = `
      <strong>Wynik human review</strong>
      <p>Status: ${escapeHtml(display(result.status))}</p>
      <p>Jira Correction: ${escapeHtml(display(result.correctionIssueKey))}</p>
    `;

    await refreshSelectedComplaint();
    await loadKpis();
  } catch (error) {
    showError(`Nie udało się zapisać decyzji human review. Sprawdź, czy reklamacja nadal istnieje po restarcie API. Szczegóły: ${error.message}`);
  } finally {
    done();
    setReviewEnabled(true, state.complaint);
  }
}

async function resetDemo() {
  clearError();
  const done = setButtonBusy(els.resetDemoButton, true, "Reset...");
  try {
    await apiRequest("/api/demo/reset", { method: "POST" });
    clearSelectedComplaint();
    renderEmptyComplaint();
    await loadKpis();
    els.nextActionHint.className = "next-action-hint";
    els.nextActionHint.textContent = "Demo zostało wyczyszczone. Wybierz scenariusz i kliknij Przetwórz scenariusz, żeby pokazać świeży pipeline.";
    document.getElementById("scenarios")?.scrollIntoView({ behavior: "smooth", block: "start" });
  } catch (error) {
    showError(`Nie udało się zresetować demo. Szczegóły: ${error.message}`);
  } finally {
    done();
  }
}

function wireNavigation() {
  const links = [...document.querySelectorAll(".nav-link")];
  const update = () => {
    const hash = location.hash || "#dashboard";
    links.forEach(link => link.classList.toggle("active", link.getAttribute("href") === hash));
  };

  window.addEventListener("hashchange", update);
  update();
}

async function init() {
  wireNavigation();
  setReviewEnabled(false);
  els.scenarioSelect.addEventListener("change", renderScenarioInfo);
  els.processScenarioButton.addEventListener("click", processSelectedScenario);
  els.refreshKpisButton.addEventListener("click", loadKpis);
  els.refreshComplaintButton.addEventListener("click", refreshSelectedComplaint);
  els.resetDemoButton.addEventListener("click", resetDemo);
  document.querySelectorAll("[data-review-decision]").forEach(button => {
    button.addEventListener("click", () => submitReview(button.dataset.reviewDecision));
  });

  await Promise.all([loadHealth(), loadScenarios(), loadKpis()]);
  if (state.selectedComplaintId) {
    await refreshSelectedComplaint({ silentMissing: true });
  }
}

init();
