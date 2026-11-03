import { copyFileSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { compareVersions, formatCsvRow, normalizeVersion, parseCsv } from "./seed-format.mjs";

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const curatedDirectory = join(repositoryRoot, "data", "curated");
const seedDirectory = join(repositoryRoot, "data", "seed");

const datasetExportedAt = "2026-11-02T23:00:00Z";
const generatedTicketCount = 250;
const firstGeneratedTicketNumber = 2001;
const hourInMilliseconds = 3_600_000;
const dayInMilliseconds = 24 * hourInMilliseconds;

const parseTimestamp = (value) => Date.parse(value.length === 10 ? `${value}T00:00:00Z` : value);
const toTimestamp = (milliseconds) => new Date(milliseconds).toISOString().replace(".000Z", "Z");
const exportedAt = parseTimestamp(datasetExportedAt);
const pick = (values, index) => values[((index % values.length) + values.length) % values.length];
const readCurated = (fileName) => readFileSync(join(curatedDirectory, fileName), "utf8");

const curatedCustomers = parseCsv(readCurated("customers.csv"));
const releases = parseCsv(readCurated("releases.csv")).sort((left, right) => compareVersions(left.version, right.version));
const curatedTicketDocument = JSON.parse(readCurated("tickets.json"));
const curatedBugDocument = JSON.parse(readCurated("bugs.json"));

const releaseTimeline = releases.map((release) => ({ version: release.version, releasedAt: parseTimestamp(release.released_at) }));

function releaseTime(version) {
  const release = releaseTimeline.find((entry) => entry.version === version);
  if (!release) throw new Error(`Unknown release ${version}`);
  return release.releasedAt;
}

function nextReleaseTime(version) {
  const index = releaseTimeline.findIndex((entry) => entry.version === version);
  return index + 1 < releaseTimeline.length ? releaseTimeline[index + 1].releasedAt : exportedAt;
}

function releaseCurrentAt(time) {
  let current = releaseTimeline[0].version;
  for (const entry of releaseTimeline) if (entry.releasedAt <= time) current = entry.version;
  return current;
}

const generatedCustomerNames = [
  ["alder", "Alder Manufacturing"],
  ["basalt", "Basalt Energy Partners"],
  ["cobalt", "Cobalt Robotics"],
  ["driftwood", "Driftwood Hospitality"],
  ["ember", "Ember Foods"],
  ["fjord", "Fjord Shipping"],
  ["granite", "Granite Mutual"],
  ["harbor", "Harbor Health Network"],
  ["iris", "Iris Biotech"],
  ["juniper", "Juniper Schools"],
  ["kiln", "Kiln Ceramics Supply"],
  ["larch", "Larch Property Group"],
  ["meridian", "Meridian Travel"],
  ["nimbus", "Nimbus Telecom"],
  ["obsidian", "Obsidian Security Services"],
  ["prairie", "Prairie Farm Cooperative"],
  ["quarry", "Quarry Building Materials"],
  ["ridgeway", "Ridgeway Transit"],
  ["saffron", "Saffron Restaurants"],
  ["talon", "Talon Aerospace"],
  ["umber", "Umber Publishing"],
  ["vantage", "Vantage Wealth"],
  ["willow", "Willow Care Homes"],
  ["yarrow", "Yarrow Pharmacy"],
  ["zephyr", "Zephyr Air Cargo"],
  ["bramble", "Bramble Outdoor"],
  ["cinder", "Cinder Steelworks"],
  ["delta-river", "Delta River Utilities"],
  ["estuary", "Estuary Water Authority"],
  ["foxglove", "Foxglove Clinics"],
];

const generatedCustomers = generatedCustomerNames.map(([accountSlug, name], index) => {
  const plan = pick(["business", "enterprise", "starter", "business", "enterprise"], index);
  return {
    id: `customer:${accountSlug}`,
    source_system: "synthetic-crm",
    source_id: `ACC-${String(index + 11).padStart(4, "0")}`,
    account_slug: accountSlug,
    name,
    plan,
    region: index === 12 ? "" : pick(["EU", "US", "UK", "APAC", "US", "EU", "LATAM"], index),
    deployment: plan === "enterprise" && index % 2 === 0 ? "dedicated" : "cloud",
    customer_since: toTimestamp(parseTimestamp("2023-02-06") + index * 21 * dayInMilliseconds).slice(0, 10),
    synthetic: "true",
  };
});

const customers = [...curatedCustomers, ...generatedCustomers];

function reportedVersionFor(customer, customerIndex, time) {
  if (customer.deployment === "cloud") return releaseCurrentAt(time);
  const upgradeLagDays = 21 + ((customerIndex * 13) % 70);
  return releaseCurrentAt(time - upgradeLagDays * dayInMilliseconds);
}

const generatedBugDefinitions = [
  { id: "BUG-151", features: ["dashboards"], introducedIn: "1.9.0", fixedIn: "1.9.1", status: "fixed", severity: "medium", title: "Dashboard image export fails for charts with more than 1,000 points", description: "Exporting a dashboard as PNG fails with 'Render timeout' when any line chart has more than 1,000 data points.", workaround: "Aggregate the chart by week before exporting." },
  { id: "BUG-153", features: ["scheduled-reports"], introducedIn: "1.9.0", fixedIn: "2.0.0", status: "fixed", severity: "low", title: "Scheduled report subject line drops non-ASCII characters", description: "Accented and non-Latin characters in scheduled report email subjects are replaced with question marks.", workaround: "Use ASCII characters in the subject template." },
  { id: "BUG-155", features: ["public-api"], introducedIn: "1.9.0", fixedIn: "2.0.1", status: "fixed", severity: "medium", title: "API returns 500 when a filter value contains a comma", description: "GET requests with filter values containing an unescaped comma fail with HTTP 500 instead of being parsed or rejected with 400.", workaround: "Percent-encode commas in filter values." },
  { id: "BUG-157", features: ["data-import"], introducedIn: null, fixedIn: "2.0.0", status: "fixed", severity: "high", title: "Import silently truncates text fields longer than 255 characters", description: "Text values longer than 255 characters are cut off during import without a warning in the import summary.", workaround: "Split long notes into multiple fields before importing." },
  { id: "BUG-159", features: ["csv-export"], introducedIn: "1.9.1", fixedIn: "2.0.2", status: "fixed", severity: "medium", title: "CSV export escapes quotes incorrectly in multi-line text fields", description: "Double quotes inside multi-line text values are not doubled, so spreadsheet tools split the row into several broken rows.", workaround: "Export as XLSX for reports with multi-line notes." },
  { id: "BUG-161", features: ["sso-login"], introducedIn: "2.0.0", fixedIn: "2.0.3", status: "fixed", severity: "high", title: "SAML signing certificate rollover not applied until the next deploy", description: "A newly uploaded IdP certificate is stored but not used for signature validation until the application is redeployed, so sign-ins fail after the IdP switches certificates.", workaround: "Keep the IdP on the old certificate until support confirms the reload." },
  { id: "BUG-163", features: ["public-api"], introducedIn: "2.0.0", fixedIn: "2.0.2", status: "fixed", severity: "high", title: "Offset pagination skips records when sorting by updated_at", description: "Records updated while an integration pages through /v2/records with sort=updated_at move between pages and are skipped.", workaround: "Sort by id when paginating with offsets." },
  { id: "BUG-165", features: ["scheduled-reports"], introducedIn: "2.0.0", fixedIn: null, status: "open", severity: "medium", title: "Scheduled report fails when the schedule owner is deactivated", description: "Schedules owned by a deactivated user fail with status failed_owner and no notification is sent to administrators.", workaround: "Transfer schedules to an active user before deactivating the owner. See KB-026." },
  { id: "BUG-167", features: ["webhooks"], introducedIn: "2.0.0", fixedIn: "2.0.1", status: "fixed", severity: "low", title: "Webhook test button always reports success", description: "The webhook test console shows 'Delivered' even when the receiver returns an error or the request times out.", workaround: "Check the delivery log for the real response code." },
  { id: "BUG-169", features: ["user-roles"], introducedIn: "2.0.1", fixedIn: null, status: "wontfix", severity: "low", title: "Role changes take up to 10 minutes to apply", description: "Permission changes on a role are cached for up to 10 minutes, so users keep their old access until the cache expires.", workaround: "Product decision: permission cache TTL is intentional for performance. Ask users to sign out and in again." },
  { id: "BUG-171", features: ["connector-sync"], introducedIn: "2.0.2", fixedIn: "2.1.0", status: "fixed", severity: "high", title: "Connector sync stalls after OAuth token refresh", description: "After the source system's OAuth access token is refreshed, the connector keeps using the expired token and every run stays in running state.", workaround: "Disable and re-enable the connector. See KB-022." },
  { id: "BUG-173", features: ["api-rate-limits"], introducedIn: "2.0.0", fixedIn: " 2.0.3", status: "fixed", severity: "medium", title: "Rate limit counter not reset at window boundary for burst requests", description: "Bursts of requests at the end of a rate limit window are counted in the next window as well, causing unexpected 429 responses.", workaround: "Spread requests evenly across the minute." },
  { id: "BUG-175", features: ["pdf-export"], introducedIn: "2.0.1", fixedIn: "2.1.0", status: "fixed", severity: "low", title: "PDF export ignores report page margins", description: "Custom page margins set in the report print layout are ignored and the default 10 mm margins are used.", workaround: "Adjust column widths instead of margins." },
  { id: "BUG-177", features: ["dashboards"], introducedIn: "2.0.2", fixedIn: "2.1.1", status: "fixed", severity: "low", title: "Dashboard date picker defaults to UTC instead of the workspace timezone", description: "The default date range on dashboards is computed in UTC, so 'Today' starts at the wrong hour for non-UTC workspaces.", workaround: "Select explicit dates instead of relative ranges." },
  { id: "BUG-179", features: ["api-tokens"], introducedIn: "2.0.3", fixedIn: null, status: "open", severity: "low", title: "Token last-used timestamp not updated for read-only requests", description: "The Last used column in Settings > API Tokens only changes when a token performs a write request.", workaround: "Use the API audit log to check read activity. See KB-029." },
  { id: "BUG-181", features: ["connector-sync"], introducedIn: "2.0", introducedEvidence: "Reported against the 2.0 line; the exact patch version was not recorded.", createdAt: "2025-12-02T10:00:00Z", fixedIn: "2.2.0", status: "fixed", severity: "medium", title: "Connector field mapping lost after renaming a source column", description: "Renaming a column in the source system removes its field mapping, so the target field stays empty on every following sync.", workaround: "Remap the field after renaming. See KB-037." },
  { id: "BUG-183", features: ["data-import"], introducedIn: null, createdAt: "2026-09-10T10:00:00Z", fixedIn: null, status: "open", severity: "low", title: "Import preview shows wrong row numbers for files with blank lines", description: "Blank lines are skipped when counting rows, so validation errors in the preview point to the wrong row numbers.", workaround: "Remove blank lines before importing." },
  { id: "BUG-185", features: ["sso-login"], introducedIn: "2.1.0", fixedIn: "2.1.1", status: "fixed", severity: "medium", title: "MFA prompt loops in Safari private browsing", description: "In Safari private windows, the MFA challenge reappears after a correct code because the session cookie cannot be stored.", workaround: "Use a normal browser window. See KB-025." },
  { id: "BUG-187", features: ["public-api"], introducedIn: "2.1.0", fixedIn: null, status: "open", severity: "low", title: "API ignores If-None-Match for /v2/reports", description: "Conditional GET requests with If-None-Match always return 200 with a full body instead of 304.", workaround: null },
  { id: "BUG-189", features: ["webhooks"], introducedIn: "2.0.0", fixedIn: "2.2.1", status: "fixed", severity: "low", title: "Webhook delivery log shows response time in the wrong unit", description: "The delivery log labels response times as milliseconds while the values are microseconds.", workaround: "Divide the displayed value by 1,000." },
  { id: "BUG-201", features: ["connector-sync"], introducedIn: "2.2.0", fixedIn: "2.3.0", status: "fixed", severity: "medium", title: "Incremental sync re-imports all rows after a daylight-saving change", description: "On the night of a daylight-saving transition the incremental sync cursor is reset and the connector re-imports every record.", workaround: "Pause connectors during the daylight-saving transition." },
  { id: "BUG-202", features: ["dashboards"], introducedIn: "2.2.0", fixedIn: "2.2.1", status: "fixed", severity: "medium", title: "Dashboard share links expire after 24 hours instead of 30 days", description: "Public share links created on 2.2.0 expire after one day although the dialog says 30 days.", workaround: "Recreate the share link daily." },
  { id: "BUG-203", features: ["api-rate-limits"], introducedIn: "2.2.0", fixedIn: "2.3.1", status: "fixed", severity: "medium", title: "429 responses missing the Retry-After header", description: "Rate-limited requests receive 429 without a Retry-After header, so clients retry immediately and stay throttled.", workaround: "Back off for 60 seconds after a 429. See KB-020." },
  { id: "BUG-205", features: ["csv-export"], introducedIn: "2.2.1", fixedIn: null, status: "open", severity: "low", title: "CSV export uses the browser timezone instead of the report timezone for timestamps", description: "Timestamp columns in CSV exports are converted to the downloading user's browser timezone instead of the report's configured timezone.", workaround: "Set the report timezone to match the users' timezone." },
  { id: "BUG-206", features: ["scheduled-reports"], introducedIn: "2.2.1", fixedIn: "2.3.1", status: "fixed", severity: "low", title: "Scheduled report runs twice when edited during execution", description: "Saving a schedule while its run is in progress queues an additional run with the new settings.", workaround: "Edit schedules only between runs." },
  { id: "BUG-208", features: ["user-roles"], introducedIn: "2.3.0", fixedIn: "2.4.0", status: "fixed", severity: "medium", title: "Bulk role assignment fails for more than 500 users", description: "Assigning a role to more than 500 selected users fails with 'Request too large' and no users are updated.", workaround: "Assign roles in batches of 500." },
  { id: "BUG-210", features: ["public-api", "user-roles"], introducedIn: "2.3.0", fixedIn: null, status: "open", severity: "low", title: "API returns 404 instead of 403 for reports in restricted folders", description: "GET /v2/reports/{id} returns 404 Not Found when the caller lacks folder access, which hides the real permission problem.", workaround: "Check folder membership when a known report returns 404. See KB-034." },
  { id: "BUG-211", features: ["connector-sync"], introducedIn: "2.3.0", fixedIn: "2.4.0", status: "fixed", severity: "low", title: "Sync status shows Completed while the last batch is still running", description: "The connector run is marked Completed before its final batch commits, so records appear a few minutes after the run ends.", workaround: "Wait five minutes after a run completes before checking records." },
  { id: "BUG-213", features: ["webhooks"], introducedIn: "2.3.1", fixedIn: "2.4.0", status: "fixed", severity: "medium", title: "Webhook signing secret rotation invalidates the old secret immediately", description: "Rotating an endpoint's signing secret invalidates the previous secret at once, so receivers reject deliveries until they are updated.", workaround: "Update the receiver immediately after rotating. See KB-027." },
  { id: "BUG-214", features: ["pdf-export", "dashboards"], introducedIn: "2.3.1", fixedIn: "2.4.1", status: "fixed", severity: "low", title: "PDF export of dashboards omits the last widget row", description: "When a dashboard is exported to PDF, the bottom row of widgets is missing if the dashboard height is not a multiple of the page height.", workaround: "Add an empty text widget at the bottom of the dashboard." },
  { id: "BUG-251", features: ["api-rate-limits"], introducedIn: "2.4.0", fixedIn: "2.4.1", status: "fixed", severity: "medium", title: "X-RateLimit-Reset header reports milliseconds instead of seconds", description: "The X-RateLimit-Reset header contains milliseconds, so clients that follow it wait 1,000 times longer than necessary.", workaround: "Divide X-RateLimit-Reset by 1,000 on 2.4.0." },
  { id: "BUG-253", features: ["user-roles", "public-api"], introducedIn: "2.4.0", fixedIn: "2.4.1", status: "fixed", severity: "high", title: "Analyst role receives 403 on reports in shared folders", description: "Users with the Analyst role get 403 Forbidden in the UI and API for reports in shared folders even when they are folder members.", workaround: "Temporarily grant the Editor folder role. See KB-034." },
  { id: "BUG-254", features: ["dashboards"], introducedIn: "2.4.0", fixedIn: null, status: "open", severity: "low", title: "Dashboard filter dropdown truncates values longer than 40 characters", description: "Long filter values are cut at 40 characters in the dropdown and values with the same prefix cannot be distinguished.", workaround: "Type in the dropdown to search. See KB-035." },
  { id: "BUG-255", features: ["scheduled-reports"], introducedIn: "2.4.0", fixedIn: "2.4.2", status: "fixed", severity: "low", title: "Scheduled report preview shows the previous run's data", description: "The preview pane in the schedule editor shows cached results from the last run instead of current data.", workaround: "Open the report itself to preview current data." },
  { id: "BUG-256", features: ["webhooks"], introducedIn: "2.4.1", fixedIn: "2.4.2", status: "fixed", severity: "medium", title: "invoice webhooks omit the occurred_at field", description: "Payloads for invoice.paid and invoice.voided events are missing occurred_at, so receivers cannot order invoice events.", workaround: "Use the delivery timestamp until upgrading." },
  { id: "BUG-257", features: ["dashboards"], introducedIn: "2.4.0", fixedIn: "2.4.2", status: "fixed", severity: "high", title: "Dashboards with more than 40 widgets take over 20 seconds to load", description: "The 2.4.0 rendering engine loads widgets sequentially, so dashboards with more than 40 widgets take over 20 seconds to become interactive.", workaround: "Split large dashboards. See KB-023." },
  { id: "BUG-258", features: ["connector-sync"], introducedIn: "2.4.1", fixedIn: null, status: "open", severity: "medium", title: "Connector sync retries indefinitely on HTTP 401 from the source", description: "When the source system returns 401, the connector retries every minute instead of pausing and asking for re-authorization.", workaround: "Disable and reconnect the connector. See KB-022." },
  { id: "BUG-259", features: ["data-import"], introducedIn: "2.4.1", fixedIn: null, status: "open", severity: "low", title: "Import rejects DD.MM.YYYY dates although the workspace locale uses them", description: "Date columns in DD.MM.YYYY format fail validation even when the workspace locale is set to German or Swiss formats.", workaround: "Use ISO dates (YYYY-MM-DD). See KB-033." },
  { id: "BUG-260", features: ["sso-login", "api-tokens"], introducedIn: "2.4.0", fixedIn: "2.4.2", status: "fixed", severity: "high", title: "SCIM deprovisioning leaves API tokens of removed users active", description: "Users deactivated through SCIM lose UI access, but their personal API tokens keep working until they expire.", workaround: "Revoke tokens of deprovisioned users manually. See KB-030." },
  { id: "BUG-261", features: ["csv-export"], introducedIn: "2.4.2", fixedIn: null, status: "open", severity: "low", title: "CSV export file name contains the report ID instead of the report name", description: "Since 2.4.2, downloaded CSV files are named like report-8f3c2a.csv instead of using the report name.", workaround: "Rename the file after download." },
];

function generatedBugCreatedAt(definition, index) {
  const introduced = normalizeVersion(definition.introducedIn);
  if (introduced.kind === "exact") return releaseTime(introduced.version) + (1 + (index % 3)) * dayInMilliseconds + 2 * hourInMilliseconds;
  if (definition.createdAt) return parseTimestamp(definition.createdAt);
  return releaseTime(normalizeVersion(definition.fixedIn).version) - 25 * dayInMilliseconds + 2 * hourInMilliseconds;
}

function generatedBugUpdatedAt(definition, createdAt, index) {
  if (definition.fixedIn) return releaseTime(normalizeVersion(definition.fixedIn).version) + hourInMilliseconds;
  if (definition.status === "wontfix") return createdAt + 7 * dayInMilliseconds;
  return Math.min(createdAt + (30 + (index % 20)) * dayInMilliseconds, exportedAt - dayInMilliseconds);
}

const generatedBugs = generatedBugDefinitions.map((definition, index) => {
  const createdAt = generatedBugCreatedAt(definition, index);
  const introduced = normalizeVersion(definition.introducedIn);
  const fixed = normalizeVersion(definition.fixedIn);
  return {
    id: `bug:${definition.id}`,
    source_system: "synthetic-issue-tracker",
    source_id: definition.id,
    title: definition.title,
    description: definition.description,
    status: definition.status,
    severity: definition.severity,
    feature_refs: definition.features,
    introduced_in: definition.introducedIn,
    introduced_in_evidence: definition.introducedIn === null ? null : (definition.introducedEvidence ?? `Regression traced to changes shipped in v${introduced.version}.`),
    fixed_in: definition.fixedIn,
    fix_evidence: definition.fixedIn === null ? null : `Fix listed in the v${fixed.version} release notes.`,
    workaround: definition.workaround,
    created_at: toTimestamp(createdAt),
    updated_at: toTimestamp(generatedBugUpdatedAt(definition, createdAt, index)),
  };
});

const bugs = [...curatedBugDocument.records, ...generatedBugs].sort((left, right) => Number(left.source_id.slice(4)) - Number(right.source_id.slice(4)));
const bugsByKey = new Map(bugs.map((bug) => [bug.source_id, bug]));

const agentHandles = ["agent.l1.02", "agent.l1.05", "agent.l1.11", "agent.l2.03", "agent.l2.08", "agent.l2.14", "agent.l1.19"];
const channels = ["portal", "email", "chat", "phone"];
const priorities = ["normal", "high", "low", "normal", "urgent"];
const placeholderValues = {
  report: ["Monthly Close", "Sales by Region", "Inventory Ageing", "Payroll Summary", "Open Invoices", "Shipment Exceptions"],
  field: ["Approved On", "Due Date", "Last Contact", "Renewal Date", "Shipped On", "Paid On"],
  endpoint: ["reports", "exports", "accounts", "webhooks", "invoices"],
  minutes: ["5-10", "10-20", "15-30", "20-45"],
  event: ["invoice.paid", "order.created", "user.updated", "report.completed", "dataset.refreshed"],
  weekday: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
};

function fillPlaceholders(text, instanceIndex, version) {
  return text
    .replaceAll("{version}", version)
    .replace(/\{(report|field|endpoint|minutes|event|weekday)\}/g, (_, name) => pick(placeholderValues[name], instanceIndex));
}

const ticketTemplates = [
  { key: "csv-serialize-confirmed", kind: "bug", bug: "BUG-231", count: 12, features: ["csv-export"], subjects: ["CSV export fails with ERR_EXPORT_SERIALIZE", "CSV download is empty since {version}", "Export failed (ERR_EXPORT_SERIALIZE) on {report} report", "0 KB CSV files from report export"], descriptions: ["Since moving to {version}, CSV export of our {report} report shows 'Export failed (ERR_EXPORT_SERIALIZE)' and downloads a 0 KB file. XLSX export of the same report works.", "Our finance users get ERR_EXPORT_SERIALIZE when exporting the {report} report to CSV. Other reports export fine."], notes: ["Report contains custom date field '{field}'. Reproduced on {version}.", "Custom date field '{field}' has empty values. Matches BUG-231 symptoms; XLSX workaround from KB-012 sent."] },
  { key: "csv-scheduled-attachment-confirmed", kind: "bug", bug: "BUG-231", count: 3, features: ["scheduled-reports", "csv-export"], subjects: ["Scheduled CSV attachment arrives empty"], descriptions: ["The daily {report} email has a 0-byte CSV attachment since our update to {version}."], notes: ["Scheduler log shows ERR_EXPORT_SERIALIZE on custom field '{field}'."] },
  { key: "csv-similar-unconfirmed", kind: "similar", bug: "BUG-231", count: 7, features: ["csv-export"], subjects: ["CSV export not working after upgrade", "Export to CSV fails for some users", "Problem with CSV download"], descriptions: ["Some users report that CSV export stopped working after the upgrade to {version}. No error message was captured.", "A colleague says the CSV export 'does nothing' since last week. We are on {version}."], notes: ["Possibly BUG-231, but no error code or report details were provided.", "Could not reproduce with the reports named by the customer.", "Waiting for the report name and a screenshot of the error."], resolutions: ["No root cause identified; customer reports exports work again."] },
  { key: "csv-delimiter-config", kind: "config", count: 6, features: ["csv-export"], subjects: ["CSV opens with all data in one column", "Exported CSV not split into columns"], descriptions: ["When we open exported CSV files in our spreadsheet tool, every row is in column A."], resolutions: ["Configuration: the spreadsheet locale expects semicolons; set the workspace CSV delimiter per KB-031."] },
  { key: "csv-timeout-confirmed", kind: "bug", bug: "BUG-238", versions: ["2.3.1", "2.3.2", "2.4.0", "2.4.1"], count: 4, features: ["csv-export"], subjects: ["Large CSV export times out", "Export timed out on full-year report"], descriptions: ["Exporting the full {report} history (several hundred thousand rows) fails after two minutes with 'Export timed out'."], notes: ["Different error from ERR_EXPORT_SERIALIZE. Suggested KB-014 and KB-039."] },
  { key: "csv-column-order-confirmed", kind: "bug", bug: "BUG-233", count: 2, features: ["csv-export"], subjects: ["CSV column order does not match the report"], descriptions: ["Columns we reordered in the viewer come out in the original order in the CSV file."] },
  { key: "csv-quote-escaping-confirmed", kind: "bug", bug: "BUG-159", count: 3, features: ["csv-export"], subjects: ["CSV rows broken by quotes in notes"], descriptions: ["Rows with multi-line notes containing quotes are split into several rows when the CSV is opened."] },
  { key: "csv-filename-confirmed", kind: "bug", bug: "BUG-261", count: 2, features: ["csv-export"], subjects: ["CSV file names changed to report IDs"], descriptions: ["Since the latest update our CSV downloads are named report-<id>.csv, which breaks our import automation."] },
  { key: "csv-timezone-confirmed", kind: "bug", bug: "BUG-205", count: 2, features: ["csv-export"], subjects: ["Timestamps in CSV export are shifted"], descriptions: ["Timestamps in exported CSV files are a few hours off compared to the {report} report on screen."] },
  { key: "api-403-scope-config", kind: "config", from: "2026-04-14", count: 12, features: ["public-api", "api-tokens"], subjects: ["403 insufficient_scope on /v2/{endpoint}", "API returns 403 for new integration", "Forbidden error calling /v2/{endpoint}"], descriptions: ["Our integration receives HTTP 403 with error 'insufficient_scope' when calling /v2/{endpoint}. Other endpoints work with the same token."], resolutions: ["Configuration: the token lacked the scope required for /v2/{endpoint}. Sent KB-015."] },
  { key: "api-403-allowlist-config", kind: "config", from: "2025-09-23", count: 4, features: ["public-api"], subjects: ["403 Forbidden from new server", "All API calls return 403 after migration"], descriptions: ["After moving our integration to a new host, every request returns 403 Forbidden with error 'ip_not_allowed'."], resolutions: ["Configuration: the caller's IP was not on the API allowlist. Sent KB-016."] },
  { key: "api-403-role-config", kind: "config", from: "2026-04-14", count: 4, features: ["public-api", "user-roles"], subjects: ["Service user gets 403 on /v2/{endpoint}"], descriptions: ["Our service token has the right scope but /v2/{endpoint} still returns 403."], resolutions: ["Configuration: the service role lacked the matching permission. Sent KB-007 and KB-015."] },
  { key: "api-403-plan-config", kind: "config", plan: "starter", count: 3, features: ["public-api"], subjects: ["403 plan_not_entitled from API"], descriptions: ["We created a token, but every API request returns 403 with 'plan_not_entitled'."], resolutions: ["Not a defect: API access requires the Business plan or higher."] },
  { key: "api-403-revoked-token-config", kind: "config", from: "2026-04-14", count: 4, features: ["public-api", "api-tokens"], subjects: ["Some API workers get 403", "403 errors from one environment only"], descriptions: ["Requests from one of our environments return 403 while others succeed with what should be the same configuration."], resolutions: ["Configuration: one environment still used a revoked token. Replaced it per KB-029."] },
  { key: "api-403-sso-legacy-config", kind: "config", from: "2025-09-23", count: 3, features: ["public-api", "sso-login"], subjects: ["403 legacy_key_disabled after SSO enforcement"], descriptions: ["Since we enforced SSO for all users, our reporting integration receives 403 'legacy_key_disabled'."], resolutions: ["Configuration: SSO enforcement disabled the legacy API key; moved the integration to a service token per KB-015."] },
  { key: "api-403-expired-confirmed", kind: "bug", bug: "BUG-244", count: 4, features: ["public-api", "api-tokens"], subjects: ["403 insufficient_scope although scopes are correct", "Nightly job fails with 403 after 90 days"], descriptions: ["Our scheduled job started failing with 403 'insufficient_scope'. Nobody changed the token or its scopes."], notes: ["Token had expired; the API should have returned 401 token_expired."] },
  { key: "api-403-shared-folder-confirmed", kind: "bug", bug: "BUG-253", count: 6, features: ["public-api", "user-roles"], subjects: ["Analysts get 403 on shared folder reports", "403 Forbidden on reports in shared folders"], descriptions: ["Since {version}, users with the Analyst role get 403 when opening reports in the shared Finance folder, both in the UI and through the API. They are folder members."], notes: ["Reproduced with an Analyst test user who is a folder member. Not a configuration problem."] },
  { key: "api-403-scope-picker-confirmed", kind: "bug", bug: "BUG-235", count: 2, features: ["api-tokens"], subjects: ["Cannot grant reports:write; integration gets 403"], descriptions: ["Our admin cannot select reports:write when creating a service token, so the integration gets 403 on report updates."] },
  { key: "api-404-restricted-folder-confirmed", kind: "bug", bug: "BUG-210", count: 2, features: ["public-api", "user-roles"], subjects: ["API returns 404 instead of 403 for restricted report"], descriptions: ["GET /v2/reports/{id} returns 404 for a report that exists. The token's user is not a member of its folder."] },
  { key: "api-403-insufficient-info", kind: "ambiguous", from: "2025-10-01", count: 4, features: ["public-api"], subjects: ["Intermittent 403 errors from API"], descriptions: ["We see occasional 403 responses in our logs. We don't have request IDs."], notes: ["No request IDs or error bodies provided.", "Asked for the error field from the response body."], resolutions: ["No root cause identified; errors stopped after the customer redeployed their integration."] },
  { key: "webhook-delay-ambiguous", kind: "ambiguous", from: "2025-09-23", count: 24, features: ["webhooks"], subjects: ["Webhook events arriving late", "Webhook delivery delays of {minutes} minutes", "{event} webhooks delayed"], descriptions: ["Since {weekday}, {event} webhooks reach our endpoint {minutes} minutes after the event. Other events seem fine.", "Our receiver logs show {event} deliveries arriving {minutes} minutes late during business hours."], notes: ["Delivery log shows first attempts on time and 5xx responses from the receiver.", "Customer suspects the 2.3.0 queue migration, but delays were also reported on 2.2.x.", "Could be related to BUG-247, but no Retry-After headers were observed. Not linked.", "No event IDs provided yet.", "Receiver sits behind a proxy that returns 502 intermittently.", "Dispatcher metrics look normal for the reported window."], resolutions: ["Delays stopped; no root cause identified.", "No root cause identified; customer scaled their receiver and delays subsided."] },
  { key: "webhook-signature-config", kind: "config", count: 4, features: ["webhooks"], subjects: ["Webhook signature verification failing"], descriptions: ["Our receiver rejects every delivery because the Ledgerline-Signature header does not match."], resolutions: ["Configuration: the receiver computed the HMAC over re-serialized JSON instead of the raw body. Sent KB-027."] },
  { key: "webhook-endpoint-config", kind: "config", count: 3, features: ["webhooks"], subjects: ["Webhook deliveries failing with TLS errors"], descriptions: ["All deliveries to our endpoint fail with 'certificate verify failed' in the delivery log."], resolutions: ["Configuration: the endpoint's TLS certificate had expired. Sent KB-028."] },
  { key: "webhook-secret-rotation-confirmed", kind: "bug", bug: "BUG-213", count: 3, features: ["webhooks"], subjects: ["Signature failures right after rotating the signing secret"], descriptions: ["We rotated the webhook signing secret and every delivery failed signature checks until we redeployed the receiver."] },
  { key: "webhook-occurred-at-confirmed", kind: "bug", bug: "BUG-256", count: 2, features: ["webhooks"], subjects: ["invoice webhooks missing occurred_at"], descriptions: ["invoice.paid payloads no longer contain occurred_at since {version}."] },
  { key: "webhook-test-button-confirmed", kind: "bug", bug: "BUG-167", count: 2, features: ["webhooks"], subjects: ["Webhook test says delivered but nothing received"], descriptions: ["The test button shows Delivered, but our endpoint never received the request."] },
  { key: "webhook-log-units-confirmed", kind: "bug", bug: "BUG-189", count: 2, features: ["webhooks"], subjects: ["Delivery log response times look wrong"], descriptions: ["The delivery log says our endpoint takes 250,000 ms to respond, which is impossible."] },
  { key: "rate-limit-config", kind: "config", from: "2025-09-23", count: 8, features: ["api-rate-limits", "public-api"], subjects: ["429 Too Many Requests during nightly sync", "Hitting API rate limits"], descriptions: ["Our nightly sync receives many 429 responses and takes hours longer than before."], resolutions: ["Not a defect: requests exceeded the plan rate limit. Shared KB-020."], markers: { 0: "unparseable-version" } },
  { key: "rate-limit-reset-confirmed", kind: "bug", bug: "BUG-251", count: 3, features: ["api-rate-limits"], subjects: ["X-RateLimit-Reset value is huge"], descriptions: ["Our client waits for hours because X-RateLimit-Reset contains values like 42000."] },
  { key: "rate-limit-retry-after-confirmed", kind: "bug", bug: "BUG-203", count: 3, features: ["api-rate-limits"], subjects: ["429 responses have no Retry-After header"], descriptions: ["Rate-limited responses do not include Retry-After, so our client cannot back off correctly."] },
  { key: "rate-limit-burst-confirmed", kind: "bug", bug: "BUG-173", count: 2, features: ["api-rate-limits"], subjects: ["Unexpected 429 below the documented limit"], descriptions: ["We send 100 requests per minute in short bursts and still get 429 responses."] },
  { key: "sso-config", kind: "config", from: "2025-09-23", count: 5, features: ["sso-login"], subjects: ["Users cannot sign in with SSO", "SAML error: Invalid signature"], descriptions: ["Several users cannot sign in through our identity provider since this morning."], resolutions: ["Configuration: the IdP was signing with a certificate not uploaded to Ledgerline. Sent KB-038.", "Configuration: the users were not assigned to the Ledgerline app in the IdP. Sent KB-005."] },
  { key: "mfa-config", kind: "config", count: 4, features: ["sso-login"], subjects: ["MFA codes rejected"], descriptions: ["One of our managers says every MFA code is rejected."], resolutions: ["Configuration: phone clock drift; enabled automatic time on the device. Sent KB-025."] },
  { key: "saml-cert-confirmed", kind: "bug", bug: "BUG-161", count: 3, features: ["sso-login"], subjects: ["SSO broken after IdP certificate rollover"], descriptions: ["We uploaded our new IdP certificate as instructed, but sign-ins fail with Invalid signature."] },
  { key: "mfa-safari-confirmed", kind: "bug", bug: "BUG-185", count: 2, features: ["sso-login"], subjects: ["MFA prompt keeps coming back in Safari"], descriptions: ["Users in Safari private windows are asked for their MFA code again and again."] },
  { key: "scim-token-confirmed", kind: "bug", bug: "BUG-260", count: 3, features: ["sso-login", "api-tokens"], subjects: ["Deprovisioned user's API token still works"], descriptions: ["A user we removed in our IdP still has a working personal API token."] },
  { key: "dashboard-slow-confirmed", kind: "bug", bug: "BUG-257", count: 8, features: ["dashboards"], subjects: ["Dashboards very slow since upgrade", "Operations dashboard takes 30 seconds to load"], descriptions: ["Since {version}, our large dashboards take 20-30 seconds to load. Small dashboards are fine."] },
  { key: "dashboard-slow-config", kind: "config", count: 4, features: ["dashboards"], subjects: ["Dashboard takes a long time to load"], descriptions: ["Our main dashboard is slow for everyone."], resolutions: ["Configuration: the dashboard had over 60 widgets on raw tables; shared the KB-023 recommendations."] },
  { key: "dashboard-date-confirmed", kind: "bug", bug: "BUG-177", count: 2, features: ["dashboards"], subjects: ["Dashboard 'Today' starts at the wrong time"], descriptions: ["The Today range on our dashboards starts at 01:00 local time."] },
  { key: "dashboard-share-confirmed", kind: "bug", bug: "BUG-202", count: 2, features: ["dashboards"], subjects: ["Shared dashboard link expired after one day"], descriptions: ["We shared a dashboard link with a partner and it stopped working the next day."] },
  { key: "dashboard-image-confirmed", kind: "bug", bug: "BUG-151", count: 2, features: ["dashboards"], subjects: ["Dashboard image export fails"], descriptions: ["Exporting our traffic dashboard as PNG fails with Render timeout."] },
  { key: "dashboard-filter-truncate-confirmed", kind: "bug", bug: "BUG-254", count: 2, features: ["dashboards"], subjects: ["Filter values cut off in dashboard dropdown"], descriptions: ["Long cost center names are cut off in the dashboard filter, so we cannot tell them apart."] },
  { key: "pagination-offset-confirmed", kind: "bug", bug: "BUG-163", count: 4, features: ["public-api"], subjects: ["Records missing when paging through the API"], descriptions: ["Our nightly sync pages through /v2/records sorted by updated_at and misses some records every night."] },
  { key: "pagination-cursor-confirmed", kind: "bug", bug: "BUG-224", count: 3, features: ["public-api"], subjects: ["Duplicate rows at page boundaries"], descriptions: ["With page size 500 we get the same row at the end of one page and the start of the next."] },
  { key: "api-500-filter-confirmed", kind: "bug", bug: "BUG-155", count: 2, features: ["public-api"], subjects: ["API returns 500 for some filters"], descriptions: ["Filtering /v2/accounts by name fails with 500 when the name contains a comma."] },
  { key: "api-etag-confirmed", kind: "bug", bug: "BUG-187", count: 1, features: ["public-api"], subjects: ["If-None-Match ignored on /v2/reports"], descriptions: ["Our cache sends If-None-Match but always receives 200 with the full body."] },
  { key: "report-owner-confirmed", kind: "bug", bug: "BUG-165", count: 4, features: ["scheduled-reports"], subjects: ["Scheduled reports stopped after an employee left"], descriptions: ["All schedules created by a former employee fail with failed_owner."] },
  { key: "report-delivery-config", kind: "config", count: 4, features: ["scheduled-reports"], subjects: ["Scheduled report not received"], descriptions: ["The weekly {report} report did not arrive for any recipient."], resolutions: ["Configuration: the recipients' mail gateway blocked the sender domain. Sent KB-011."] },
  { key: "report-subject-confirmed", kind: "bug", bug: "BUG-153", count: 2, features: ["scheduled-reports"], subjects: ["Question marks in scheduled report subject"], descriptions: ["Accented characters in our report subject lines show up as question marks."] },
  { key: "report-double-run-confirmed", kind: "bug", bug: "BUG-206", count: 2, features: ["scheduled-reports"], subjects: ["Scheduled report sent twice after editing"], descriptions: ["We edited a schedule while it was running and recipients got two emails."] },
  { key: "report-preview-confirmed", kind: "bug", bug: "BUG-255", count: 2, features: ["scheduled-reports"], subjects: ["Schedule preview shows old numbers"], descriptions: ["The preview in the schedule editor shows last week's data."] },
  { key: "sync-stall-confirmed", kind: "bug", bug: "BUG-171", count: 4, features: ["connector-sync"], subjects: ["CRM connector stuck in running state"], descriptions: ["Our CRM connector has been in running state for a day and no new records arrive."] },
  { key: "sync-full-reimport-confirmed", kind: "bug", bug: "BUG-201", count: 2, features: ["connector-sync"], subjects: ["Connector re-imported everything overnight"], descriptions: ["Last night's sync re-imported all 2 million records instead of the changes."] },
  { key: "sync-status-confirmed", kind: "bug", bug: "BUG-211", count: 2, features: ["connector-sync"], subjects: ["Sync says Completed but records are missing"], descriptions: ["The run shows Completed, but the last records appear only a few minutes later."] },
  { key: "sync-401-loop-confirmed", kind: "bug", bug: "BUG-258", count: 2, features: ["connector-sync"], subjects: ["Connector retrying every minute with 401"], descriptions: ["Our ERP security team sees a failed login from Ledgerline every minute."] },
  { key: "sync-delay-ambiguous", kind: "ambiguous", from: "2026-02-10", count: 5, features: ["connector-sync"], subjects: ["CRM records syncing late", "ERP changes take hours to appear"], descriptions: ["Changes made in our source system take two to four hours to appear in Ledgerline."], notes: ["Run history shows successful runs; source system export lag suspected.", "No run IDs provided.", "Runs complete on schedule; cannot tell whether the delay is on the source side."], resolutions: ["No root cause identified; delays no longer observed."] },
  { key: "sync-credentials-config", kind: "config", from: "2025-11-25", count: 5, features: ["connector-sync"], subjects: ["Connector shows Reconnect required"], descriptions: ["Our connector stopped syncing and asks us to reconnect."], resolutions: ["Configuration: the source system password was changed; connector re-authorized. Sent KB-022."], markers: { 1: "duplicate-snapshot" } },
  { key: "sync-mapping-config", kind: "config", from: "2026-04-14", count: 3, features: ["connector-sync"], subjects: ["Connector fields empty after a source change"], descriptions: ["Some fields stopped syncing after our CRM admin renamed columns."], resolutions: ["Configuration: remapped the renamed source fields. Sent KB-037."] },
  { key: "sync-mapping-confirmed", kind: "bug", bug: "BUG-181", versions: ["2.0.3", "2.1.0", "2.1.1"], count: 2, features: ["connector-sync"], subjects: ["Field mapping disappeared after renaming a column"], descriptions: ["After renaming a column in our CRM, its mapping vanished from the connector settings."] },
  { key: "import-config", kind: "config", count: 4, features: ["data-import"], subjects: ["Import fails with Invalid date"], descriptions: ["Our product import fails with 'Invalid date' on most rows."], resolutions: ["Configuration: dates did not match the workspace locale; switched to ISO dates per KB-033."] },
  { key: "import-truncate-confirmed", kind: "bug", bug: "BUG-157", count: 2, features: ["data-import"], subjects: ["Imported descriptions are cut off"], descriptions: ["Long product descriptions are cut off after import with no warning."] },
  { key: "import-preview-confirmed", kind: "bug", bug: "BUG-183", versions: ["2.4.0", "2.4.1"], count: 1, features: ["data-import"], subjects: ["Import preview points to wrong rows"], descriptions: ["The preview says row 40 is invalid, but the problem is on row 43."] },
  { key: "import-date-confirmed", kind: "bug", bug: "BUG-259", count: 2, features: ["data-import"], subjects: ["German date format rejected by import"], descriptions: ["Dates like 24.10.2026 are rejected although our workspace locale is German."] },
  { key: "pdf-margins-confirmed", kind: "bug", bug: "BUG-175", count: 2, features: ["pdf-export"], subjects: ["PDF margins ignored"], descriptions: ["Our custom margins have no effect on exported PDFs."] },
  { key: "pdf-dashboard-confirmed", kind: "bug", bug: "BUG-214", count: 2, features: ["pdf-export", "dashboards"], subjects: ["Last row missing in dashboard PDF"], descriptions: ["The bottom widgets of our board dashboard are missing from the PDF export."] },
  { key: "roles-delay-confirmed", kind: "bug", bug: "BUG-169", count: 2, features: ["user-roles"], subjects: ["Permission change not applied right away"], descriptions: ["We removed a permission from a role, but users kept access for several minutes."] },
  { key: "roles-bulk-confirmed", kind: "bug", bug: "BUG-208", count: 2, features: ["user-roles"], subjects: ["Bulk role assignment fails"], descriptions: ["Assigning a role to all 800 store managers fails with Request too large."] },
  { key: "token-last-used-confirmed", kind: "bug", bug: "BUG-179", count: 1, features: ["api-tokens"], subjects: ["Token last used date never changes"], descriptions: ["Our read-only reporting token shows a last used date from months ago, although it runs daily."] },
  { key: "uncategorized-question", kind: "config", count: 1, features: [], subjects: ["Question about our renewal invoice"], descriptions: ["Our renewal invoice lists more seats than we use. Who can help?"], resolutions: ["Not a defect: forwarded to the billing team."] },
];

function affectedVersions(bug, versionOverride) {
  if (versionOverride) return versionOverride;
  const introduced = normalizeVersion(bug.introduced_in);
  const fixed = normalizeVersion(bug.fixed_in);
  const candidates = releaseTimeline
    .map((entry) => entry.version)
    .filter((version) => (introduced.kind !== "exact" || compareVersions(version, introduced.version) >= 0) && (fixed.kind !== "exact" || compareVersions(version, fixed.version) < 0));
  return introduced.kind === "exact" ? candidates : candidates.slice(-2);
}

function timeWithin(windowStart, windowEnd, seed) {
  const spanHours = Math.max(1, Math.floor((windowEnd - windowStart) / hourInMilliseconds) - 12);
  return windowStart + ((seed * 37) % spanHours) * hourInMilliseconds + ((seed * 13) % 60) * 60_000;
}

function chooseCustomer(time, seed, plan) {
  const candidates = generatedCustomers
    .map((customer, index) => ({ customer, index: index + curatedCustomers.length }))
    .filter(({ customer }) => parseTimestamp(customer.customer_since) <= time && (!plan || customer.plan === plan));
  if (candidates.length === 0) throw new Error(`No active customer at ${toTimestamp(time)}`);
  return pick(candidates, seed);
}

function buildBugTicket(template, templateIndex, instanceIndex) {
  const bug = bugsByKey.get(template.bug);
  if (!bug) throw new Error(`${template.key} references unknown ${template.bug}`);
  const versions = affectedVersions(bug, template.versions);
  const version = pick(versions, instanceIndex * 3 + templateIndex);
  const windowStart = releaseTime(version) + 6 * hourInMilliseconds;
  const windowEnd = Math.min(nextReleaseTime(version), exportedAt - 2 * dayInMilliseconds);
  const seed = templateIndex * 7 + instanceIndex * 11 + 3;
  const createdAt = timeWithin(windowStart, windowEnd, seed);
  const { customer } = chooseCustomer(createdAt, templateIndex * 5 + instanceIndex * 11, template.plan);
  const ticket = baseTicket(template, instanceIndex, customer, createdAt, version);
  if (template.kind === "similar") return applyAmbiguousOutcome(ticket, template, instanceIndex, createdAt);
  const bugCreatedAt = parseTimestamp(bug.created_at);
  const confirmedAt = Math.min(Math.max(createdAt + (1 + (instanceIndex % 3)) * dayInMilliseconds, bugCreatedAt + hourInMilliseconds), exportedAt - 2 * hourInMilliseconds);
  ticket.confirmed_bug_links = [{ bug_ref: bug.source_id, confirmed_by: pick(agentHandles, seed), confirmed_at: toTimestamp(Math.max(confirmedAt, createdAt + hourInMilliseconds)) }];
  ticket.internal_notes = template.notes ? fillPlaceholders(pick(template.notes, instanceIndex), instanceIndex, version) : `Reproduced on ${version}.`;
  const linkTime = parseTimestamp(ticket.confirmed_bug_links[0].confirmed_at);
  const fixed = normalizeVersion(bug.fixed_in);
  if (bug.status === "fixed" && fixed.kind === "exact" && releaseTime(fixed.version) + 3 * dayInMilliseconds < exportedAt) {
    const fixReleasedAt = releaseTime(fixed.version);
    if (instanceIndex % 6 === 5) {
      ticket.status = "pending_customer";
      ticket.internal_notes = `${ticket.internal_notes} Fix available in ${fixed.version}; customer has not confirmed after upgrading.`;
      ticket.updated_at = toTimestamp(Math.min(Math.max(fixReleasedAt, linkTime) + (2 + (instanceIndex % 5)) * dayInMilliseconds, exportedAt - hourInMilliseconds));
    } else {
      const resolvedAt = Math.min(Math.max(fixReleasedAt + (1 + (instanceIndex % 6)) * dayInMilliseconds, linkTime + hourInMilliseconds), exportedAt - hourInMilliseconds);
      ticket.status = "resolved";
      ticket.resolution = instanceIndex % 2 === 0 ? `Fixed in ${fixed.version}. Customer confirmed after upgrading.` : `Fixed in ${fixed.version}. Customer verified the fix.`;
      ticket.resolved_at = toTimestamp(resolvedAt);
      ticket.updated_at = ticket.resolved_at;
    }
  } else if (bug.status === "wontfix") {
    const resolvedAt = Math.min(linkTime + 2 * dayInMilliseconds, exportedAt - hourInMilliseconds);
    ticket.status = "closed";
    ticket.resolution = "Product decision: working as designed (wontfix). Workaround shared.";
    ticket.resolved_at = toTimestamp(resolvedAt);
    ticket.updated_at = ticket.resolved_at;
  } else {
    ticket.status = instanceIndex % 3 === 0 ? "pending_customer" : "open";
    ticket.updated_at = toTimestamp(Math.min(linkTime + (1 + (instanceIndex % 10)) * dayInMilliseconds, exportedAt - hourInMilliseconds));
  }
  return ticket;
}

function buildWindowedTicket(template, templateIndex, instanceIndex) {
  const windowStart = parseTimestamp(template.from ?? "2025-08-01");
  const windowEnd = exportedAt - dayInMilliseconds;
  const seed = templateIndex * 7 + instanceIndex * 11 + 3;
  const createdAt = timeWithin(windowStart, windowEnd, seed);
  const { customer, index } = chooseCustomer(createdAt, templateIndex * 5 + instanceIndex * 11, template.plan);
  const version = reportedVersionFor(customer, index, createdAt);
  const ticket = baseTicket(template, instanceIndex, customer, createdAt, version);
  if (template.kind === "ambiguous") return applyAmbiguousOutcome(ticket, template, instanceIndex, createdAt);
  const resolvedAt = createdAt + (2 + (instanceIndex % 20)) * hourInMilliseconds;
  ticket.status = instanceIndex % 5 === 4 ? "closed" : "resolved";
  ticket.resolution = fillPlaceholders(pick(template.resolutions, instanceIndex), instanceIndex, version);
  ticket.resolved_at = toTimestamp(resolvedAt);
  ticket.updated_at = ticket.resolved_at;
  return ticket;
}

function applyAmbiguousOutcome(ticket, template, instanceIndex, createdAt) {
  const outcome = pick(["open", "resolved", "closed", "open", "resolved"], instanceIndex);
  ticket.internal_notes = fillPlaceholders(pick(template.notes, instanceIndex), instanceIndex, ticket.reported_version);
  const closingTime = Math.min(createdAt + (3 + (instanceIndex % 12)) * dayInMilliseconds, exportedAt - hourInMilliseconds);
  ticket.status = outcome;
  if (outcome === "open") {
    ticket.updated_at = toTimestamp(Math.min(createdAt + (1 + (instanceIndex % 6)) * dayInMilliseconds, exportedAt - hourInMilliseconds));
  } else {
    ticket.resolution = outcome === "closed" ? "Closed without customer response." : pick(template.resolutions, instanceIndex);
    ticket.resolved_at = toTimestamp(closingTime);
    ticket.updated_at = ticket.resolved_at;
  }
  return ticket;
}

function baseTicket(template, instanceIndex, customer, createdAt, version) {
  return {
    templateKey: template.key,
    templateKind: template.kind,
    marker: template.markers?.[instanceIndex] ?? null,
    sortTime: createdAt,
    id: null,
    source_system: "synthetic-helpdesk",
    source_id: null,
    customer_ref: customer.account_slug,
    subject: fillPlaceholders(pick(template.subjects, instanceIndex), instanceIndex, version),
    description: fillPlaceholders(pick(template.descriptions, instanceIndex), instanceIndex, version),
    status: "open",
    priority: pick(priorities, instanceIndex + template.key.length),
    channel: pick(channels, instanceIndex + template.key.length * 3),
    reported_version: version,
    feature_refs: template.features,
    confirmed_bug_links: [],
    internal_notes: null,
    resolution: null,
    created_at: toTimestamp(createdAt),
    updated_at: toTimestamp(createdAt),
    resolved_at: null,
  };
}

const generatedTickets = ticketTemplates.flatMap((template, templateIndex) =>
  Array.from({ length: template.count }, (_, instanceIndex) =>
    template.kind === "bug" || template.kind === "similar" ? buildBugTicket(template, templateIndex, instanceIndex) : buildWindowedTicket(template, templateIndex, instanceIndex),
  ),
);

if (generatedTickets.length !== generatedTicketCount) throw new Error(`Templates produce ${generatedTickets.length} tickets, expected ${generatedTicketCount}`);

generatedTickets.sort((left, right) => left.sortTime - right.sortTime || left.templateKey.localeCompare(right.templateKey));

const capitalize = (text) => text.charAt(0).toUpperCase() + text.slice(1);
const duplicateSnapshots = [];
const generatedAnomalies = [];

generatedTickets.forEach((ticket, index) => {
  ticket.source_id = `TCK-${firstGeneratedTicketNumber + index}`;
  ticket.id = `ticket:${ticket.source_id}`;
  if (index % 9 === 4) ticket.customer_ref = ticket.customer_ref.toUpperCase();
  else if (index % 14 === 6) ticket.customer_ref = `${ticket.customer_ref} `;
  else if (index % 23 === 10) ticket.customer_ref = ` ${capitalize(ticket.customer_ref)}`;
  const version = ticket.reported_version;
  if (index % 11 === 3) ticket.reported_version = `v${version}`;
  else if (index % 13 === 5) ticket.reported_version = version.split(".").slice(0, 2).join(".");
  else if (index % 17 === 8) ticket.reported_version = ` ${version} `;
  else if (index % 19 === 9 && ticket.templateKind !== "bug") ticket.reported_version = null;
  if (ticket.marker === "unparseable-version") {
    ticket.reported_version = "latest";
    generatedAnomalies.push(`unparseable-version:${ticket.source_id}`);
  }
  if (ticket.marker === "duplicate-snapshot") {
    duplicateSnapshots.push({ ...ticket, status: "open", resolution: null, resolved_at: null, updated_at: ticket.created_at, internal_notes: "Collecting details from the customer." });
    generatedAnomalies.push(`duplicate-ticket-record:${ticket.source_id}`);
  }
  if (ticket.feature_refs.length === 0) generatedAnomalies.push(`missing-feature-refs:${ticket.source_id}`);
});

const withoutGenerationFields = ({ templateKey, templateKind, marker, sortTime, ...ticket }) => ticket;
const tickets = [...curatedTicketDocument.records, ...generatedTickets.map(withoutGenerationFields), ...duplicateSnapshots.map(withoutGenerationFields)];

rmSync(seedDirectory, { recursive: true, force: true });
mkdirSync(join(seedDirectory, "knowledge"), { recursive: true });

const customerColumns = Object.keys(curatedCustomers[0]);
writeFileSync(join(seedDirectory, "customers.csv"), `${[customerColumns.join(","), ...customers.map((customer) => formatCsvRow(customerColumns.map((column) => customer[column])))].join("\n")}\n`);
copyFileSync(join(curatedDirectory, "features.csv"), join(seedDirectory, "features.csv"));
copyFileSync(join(curatedDirectory, "releases.csv"), join(seedDirectory, "releases.csv"));
writeFileSync(join(seedDirectory, "tickets.json"), `${JSON.stringify({ ...curatedTicketDocument, records: tickets }, null, 2)}\n`);
writeFileSync(join(seedDirectory, "bugs.json"), `${JSON.stringify({ ...curatedBugDocument, records: bugs }, null, 2)}\n`);
for (const fileName of readdirSync(join(curatedDirectory, "knowledge")).sort()) {
  copyFileSync(join(curatedDirectory, "knowledge", fileName), join(seedDirectory, "knowledge", fileName));
}

console.log(`customers=${customers.length} releases=${releases.length} tickets=${tickets.length} bugs=${bugs.length}`);
console.log(`generated anomalies: ${generatedAnomalies.join(", ")}`);
