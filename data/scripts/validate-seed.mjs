import { readFileSync, readdirSync } from "node:fs";
import { createHash } from "node:crypto";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { compareVersions, normalizeBugRef, normalizeCustomerRef, normalizeFeatureRef, normalizeVersion, parseCsv } from "./seed-format.mjs";

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const seedDirectory = join(repositoryRoot, "data", "seed");
const knowledgeDirectory = join(seedDirectory, "knowledge");

const expectedCounts = {
  customers: 40,
  features: 12,
  releases: 16,
  ticketRecords: 302,
  uniqueTickets: 300,
  bugs: 60,
  knowledgeArticles: 30,
};

const expectedAnomalies = new Set([
  "duplicate-ticket-record:TCK-1023",
  "duplicate-ticket-record:TCK-2180",
  "unresolved-bug-ref:TCK-1002->BUG-190",
  "ambiguous-release-version:BUG-215.introduced_in=2.2",
  "ambiguous-release-version:BUG-181.introduced_in=2.0",
  "unparseable-version:TCK-2144",
  "missing-feature-refs:TCK-2097",
]);

const acmeTicketId = "TCK-1046";
const csvExportBugId = "BUG-231";
const curatedCsvExportConfirmedTickets = ["TCK-1017", "TCK-1018", "TCK-1020", "TCK-1024", "TCK-1027", "TCK-1033"];
const curatedUnconfirmedSimilarCsvTickets = ["TCK-1034", "TCK-1046", "TCK-1050"];
const permissionDefectBugs = new Set(["BUG-210", "BUG-235", "BUG-244", "BUG-253"]);
const unlinkedForbiddenResolutionPattern = /^(Configuration|Not a defect|No root cause|Closed without)/;
const webhookDelayPattern = /\b(delay\w*|late|latency)\b/i;
const forbiddenPattern = /\b403\b/;
const unconfirmedWebhookBugId = "BUG-247";

const results = [];
const observedAnomalies = new Set();

function check(description, passed, detail = "") {
  results.push({ description, passed: Boolean(passed), detail });
}

function recordAnomaly(key) {
  observedAnomalies.add(key);
}

function loadCsv(fileName) {
  try {
    const rows = parseCsv(readFileSync(join(seedDirectory, fileName), "utf8"));
    check(`${fileName} parses as CSV`, true);
    return rows;
  } catch (error) {
    check(`${fileName} parses as CSV`, false, error.message);
    return [];
  }
}

function loadJson(path) {
  const displayPath = relative(repositoryRoot, path).replaceAll("\\", "/");
  try {
    const document = JSON.parse(readFileSync(path, "utf8"));
    check(`${displayPath} parses as JSON`, true);
    return document;
  } catch (error) {
    check(`${displayPath} parses as JSON`, false, error.message);
    return { records: [] };
  }
}

function findDuplicates(values) {
  const seen = new Set();
  const duplicates = new Set();
  for (const value of values) {
    if (seen.has(value)) duplicates.add(value);
    seen.add(value);
  }
  return [...duplicates];
}

function failingIds(records, predicate) {
  return records.filter((record) => !predicate(record)).map((record) => record.source_id);
}

function checkAll(description, records, predicate) {
  const failures = failingIds(records, predicate);
  check(description, failures.length === 0, failures.slice(0, 10).join(", "));
}

const sha256 = (path) => createHash("sha256").update(readFileSync(path)).digest("hex");

const customers = loadCsv("customers.csv");
const features = loadCsv("features.csv");
const releases = loadCsv("releases.csv");
const ticketDocument = loadJson(join(seedDirectory, "tickets.json"));
const bugDocument = loadJson(join(seedDirectory, "bugs.json"));
const knowledgeDocument = loadJson(join(knowledgeDirectory, "manifest.json"));

const ticketRecords = ticketDocument.records;
const bugs = bugDocument.records;
const articles = knowledgeDocument.records;
const exportedAt = ticketDocument.exported_at;

check(`customers.csv has ${expectedCounts.customers} rows`, customers.length === expectedCounts.customers, `found ${customers.length}`);
check(`features.csv has ${expectedCounts.features} rows`, features.length === expectedCounts.features, `found ${features.length}`);
check(`releases.csv has ${expectedCounts.releases} rows`, releases.length === expectedCounts.releases, `found ${releases.length}`);
check(`tickets.json has ${expectedCounts.ticketRecords} raw records`, ticketRecords.length === expectedCounts.ticketRecords, `found ${ticketRecords.length}`);
check(`bugs.json has ${expectedCounts.bugs} records`, bugs.length === expectedCounts.bugs, `found ${bugs.length}`);
check(`knowledge manifest has ${expectedCounts.knowledgeArticles} articles`, articles.length === expectedCounts.knowledgeArticles, `found ${articles.length}`);
check("all JSON exports share one exported_at", exportedAt && bugDocument.exported_at === exportedAt && knowledgeDocument.exported_at === exportedAt);

for (const [fileName, rows] of [["customers.csv", customers], ["features.csv", features], ["releases.csv", releases]]) {
  check(`${fileName} rows are all labeled synthetic`, rows.every((row) => row.synthetic === "true"));
}
for (const [fileName, document] of [["tickets.json", ticketDocument], ["bugs.json", bugDocument], ["knowledge/manifest.json", knowledgeDocument]]) {
  check(`${fileName} is labeled synthetic`, document.synthetic === true && typeof document.disclaimer === "string");
}

const markdownFiles = readdirSync(knowledgeDirectory).filter((fileName) => fileName.endsWith(".md")).sort();
const manifestFiles = articles.map((article) => article.file).sort();
check("every knowledge Markdown file is listed in the manifest and vice versa", JSON.stringify(markdownFiles) === JSON.stringify(manifestFiles), `files=${markdownFiles.length} manifest=${manifestFiles.length}`);
checkAll("every knowledge Markdown file is labeled synthetic and names its article ID", articles, (article) => {
  const content = readFileSync(join(knowledgeDirectory, article.file), "utf8");
  return content.includes("Synthetic article") && content.includes(article.source_id);
});

checkAll("customer ids follow customer:<account_slug>", customers, (customer) => customer.id === `customer:${customer.account_slug}` && customer.account_slug === normalizeCustomerRef(customer.account_slug));
checkAll("feature ids follow feature:<source_id>", features, (feature) => feature.id === `feature:${feature.source_id}`);
checkAll("release ids follow release:<version> with source_id v<version>", releases, (release) => release.id === `release:${release.version}` && release.source_id === `v${release.version}` && normalizeVersion(release.version).kind === "exact");
checkAll("ticket ids follow ticket:<source_id>", ticketRecords, (ticket) => ticket.id === `ticket:${ticket.source_id}`);
checkAll("bug ids follow bug:<source_id>", bugs, (bug) => bug.id === `bug:${bug.source_id}`);
checkAll("knowledge article ids follow kb:<source_id>", articles, (article) => article.id === `kb:${article.source_id}`);

for (const [entityName, ids] of [
  ["customer", customers.map((customer) => customer.id)],
  ["customer source", customers.map((customer) => customer.source_id)],
  ["feature", features.map((feature) => feature.id)],
  ["release", releases.map((release) => release.id)],
  ["bug", bugs.map((bug) => bug.id)],
  ["knowledge article", articles.map((article) => article.id)],
]) {
  const duplicates = findDuplicates(ids);
  check(`${entityName} ids are unique`, duplicates.length === 0, duplicates.join(", "));
}

for (const ticketId of findDuplicates(ticketRecords.map((ticket) => ticket.id))) recordAnomaly(`duplicate-ticket-record:${ticketId.replace("ticket:", "")}`);

const effectiveTickets = new Map();
for (const ticket of ticketRecords) {
  const existing = effectiveTickets.get(ticket.source_id);
  if (!existing || ticket.updated_at > existing.updated_at) effectiveTickets.set(ticket.source_id, ticket);
}
const tickets = [...effectiveTickets.values()];
check(`${expectedCounts.uniqueTickets} unique tickets after keeping the latest updated_at per source_id`, tickets.length === expectedCounts.uniqueTickets, `found ${tickets.length}`);

const customersBySlug = new Map(customers.map((customer) => [customer.account_slug, customer]));
const featuresBySlug = new Map(features.map((feature) => [feature.source_id, feature]));
const releasesByVersion = new Map(releases.map((release) => [release.version, release]));
const bugsByKey = new Map(bugs.map((bug) => [bug.source_id, bug]));
const ticketsByKey = new Map(tickets.map((ticket) => [ticket.source_id, ticket]));

checkAll("every ticket customer_ref resolves after trim and lowercase", tickets, (ticket) => customersBySlug.has(normalizeCustomerRef(ticket.customer_ref)));
checkAll("every ticket was created on or after its customer's customer_since date", tickets, (ticket) => {
  const customer = customersBySlug.get(normalizeCustomerRef(ticket.customer_ref));
  return customer && ticket.created_at.slice(0, 10) >= customer.customer_since;
});

const unresolvedFeatureRefs = [
  ...tickets.flatMap((ticket) => ticket.feature_refs.map((ref) => [ticket.source_id, ref])),
  ...bugs.flatMap((bug) => bug.feature_refs.map((ref) => [bug.source_id, ref])),
  ...articles.flatMap((article) => article.feature_refs.map((ref) => [article.source_id, ref])),
].filter(([, ref]) => !featuresBySlug.has(normalizeFeatureRef(ref)));
check("every feature_ref in tickets, bugs and articles resolves", unresolvedFeatureRefs.length === 0, unresolvedFeatureRefs.map((pair) => pair.join("->")).join(", "));

for (const ticket of tickets) if (ticket.feature_refs.length === 0) recordAnomaly(`missing-feature-refs:${ticket.source_id}`);
checkAll("every bug and article references at least one feature", [...bugs, ...articles], (record) => record.feature_refs.length > 0);
const referencedFeatures = new Set([...tickets, ...bugs, ...articles].flatMap((record) => record.feature_refs.map(normalizeFeatureRef)));
checkAll("every feature is referenced by at least one ticket, bug or article", features, (feature) => referencedFeatures.has(feature.source_id));

for (const ticket of tickets) {
  for (const link of ticket.confirmed_bug_links) {
    const bugKey = normalizeBugRef(link.bug_ref);
    if (!bugsByKey.has(bugKey)) recordAnomaly(`unresolved-bug-ref:${ticket.source_id}->${bugKey}`);
  }
}
checkAll("every confirmed link has confirmed_by and a confirmed_at after both records exist", tickets, (ticket) => ticket.confirmed_bug_links.every((link) => {
  const bug = bugsByKey.get(normalizeBugRef(link.bug_ref));
  return !bug || (link.confirmed_by && link.confirmed_at >= ticket.created_at && link.confirmed_at >= bug.created_at && link.confirmed_at <= exportedAt);
}));

const unresolvedArticleBugRefs = articles.flatMap((article) => article.bug_refs.map((ref) => [article.source_id, ref])).filter(([, ref]) => !bugsByKey.has(normalizeBugRef(ref)));
check("every knowledge article bug_ref resolves", unresolvedArticleBugRefs.length === 0, unresolvedArticleBugRefs.map((pair) => pair.join("->")).join(", "));

function resolveBugRelease(bug, fieldName) {
  const parsed = normalizeVersion(bug[fieldName]);
  if (parsed.kind === "missing") return null;
  if (parsed.kind !== "exact") {
    recordAnomaly(`ambiguous-release-version:${bug.source_id}.${fieldName}=${bug[fieldName]}`);
    return null;
  }
  if (!releasesByVersion.has(parsed.version)) {
    check(`${bug.source_id}.${fieldName} resolves to a release`, false, bug[fieldName]);
    return null;
  }
  return parsed.version;
}

const bugReleaseEvidence = new Map(bugs.map((bug) => [bug.source_id, { introducedIn: resolveBugRelease(bug, "introduced_in"), fixedIn: resolveBugRelease(bug, "fixed_in") }]));

checkAll("bug status is open, fixed or wontfix and is fixed exactly when fixed_in is set", bugs, (bug) => (bug.status === "fixed") === (bug.fixed_in !== null) && ["open", "fixed", "wontfix"].includes(bug.status));
checkAll("bug release fields carry evidence text exactly when they are set", bugs, (bug) => (bug.introduced_in === null) === (bug.introduced_in_evidence === null) && (bug.fixed_in === null) === (bug.fix_evidence === null));
checkAll("bugs are fixed after they were introduced", bugs, (bug) => {
  const { introducedIn, fixedIn } = bugReleaseEvidence.get(bug.source_id);
  return !introducedIn || !fixedIn || compareVersions(fixedIn, introducedIn) > 0;
});
checkAll("bug timestamps are ordered and not after the export", bugs, (bug) => bug.created_at <= bug.updated_at && bug.updated_at <= exportedAt);
checkAll("fixed bugs were updated no earlier than their fix release", bugs, (bug) => {
  const { fixedIn } = bugReleaseEvidence.get(bug.source_id);
  return !fixedIn || bug.updated_at >= releasesByVersion.get(fixedIn).released_at;
});

for (const release of releases) {
  const mentionedBugs = [...release.notes.matchAll(/BUG-\d+/g)].map((match) => match[0]).sort();
  const bugsFixedHere = bugs.filter((bug) => bugReleaseEvidence.get(bug.source_id).fixedIn === release.version).map((bug) => bug.source_id).sort();
  check(`release ${release.version} notes list exactly the bugs whose fixed_in is ${release.version}`, JSON.stringify(mentionedBugs) === JSON.stringify(bugsFixedHere), `notes=[${mentionedBugs}] bugs=[${bugsFixedHere}]`);
}

const releaseVersionsAscending = releases.map((release) => release.version).sort(compareVersions);
check("release dates strictly increase with version", releaseVersionsAscending.every((version, index) => index === 0 || releasesByVersion.get(version).released_at > releasesByVersion.get(releaseVersionsAscending[index - 1]).released_at));
check("no release is dated after the export", releases.every((release) => release.released_at <= exportedAt));

function versionRangeOverlapsBug(reportedVersion, evidence) {
  const parsed = normalizeVersion(reportedVersion);
  if (parsed.kind === "missing" || parsed.kind === "unparseable") return true;
  const candidates = parsed.kind === "exact" ? [parsed.version] : releaseVersionsAscending.filter((version) => version.startsWith(`${parsed.line}.`));
  return candidates.some((version) => (!evidence.introducedIn || compareVersions(version, evidence.introducedIn) >= 0) && (!evidence.fixedIn || compareVersions(version, evidence.fixedIn) < 0));
}

checkAll("ticket statuses are valid and resolved_at is set exactly for resolved or closed tickets", tickets, (ticket) => ["open", "pending_customer", "resolved", "closed"].includes(ticket.status) && (["resolved", "closed"].includes(ticket.status) ? ticket.resolved_at !== null : ticket.resolved_at === null));
checkAll("ticket timestamps are ordered and not after the export", tickets, (ticket) => ticket.created_at <= ticket.updated_at && ticket.updated_at <= exportedAt && (ticket.resolved_at === null || (ticket.resolved_at >= ticket.created_at && ticket.resolved_at <= exportedAt)));
for (const ticket of tickets) {
  if (normalizeVersion(ticket.reported_version).kind === "unparseable") recordAnomaly(`unparseable-version:${ticket.source_id}`);
}
checkAll("exact ticket versions are known releases", tickets, (ticket) => {
  const parsed = normalizeVersion(ticket.reported_version);
  return parsed.kind !== "exact" || releasesByVersion.has(parsed.version);
});
checkAll("exact ticket versions were already released when the ticket was created", tickets, (ticket) => {
  const parsed = normalizeVersion(ticket.reported_version);
  return parsed.kind !== "exact" || !releasesByVersion.has(parsed.version) || releasesByVersion.get(parsed.version).released_at <= ticket.created_at;
});
checkAll("ticket versions are inside the affected range of every confirmed bug", tickets, (ticket) => ticket.confirmed_bug_links.every((link) => {
  const bugKey = normalizeBugRef(link.bug_ref);
  return !bugsByKey.has(bugKey) || versionRangeOverlapsBug(ticket.reported_version, bugReleaseEvidence.get(bugKey));
}));
checkAll("knowledge articles are not dated after the export", articles, (article) => article.updated_at <= exportedAt);

const confirmedTicketsForBug = (bugKey) => tickets.filter((ticket) => ticket.confirmed_bug_links.some((link) => normalizeBugRef(link.bug_ref) === bugKey)).map((ticket) => ticket.source_id).sort();
const ticketCustomer = (ticketKey) => normalizeCustomerRef(ticketsByKey.get(ticketKey).customer_ref);

const acmeTicket = ticketsByKey.get(acmeTicketId);
const csvExportBug = bugsByKey.get(csvExportBugId);
const csvConfirmedTickets = confirmedTicketsForBug(csvExportBugId);
const csvConfirmedCustomers = new Set(csvConfirmedTickets.map(ticketCustomer));
check("Scenario A: Acme customer exists", customersBySlug.has("acme"));
check("Scenario A: Acme ticket belongs to Acme, concerns csv-export and reports version 2.3", acmeTicket && normalizeCustomerRef(acmeTicket.customer_ref) === "acme" && acmeTicket.feature_refs.includes("csv-export") && acmeTicket.reported_version === "2.3");
check("Scenario A: Acme ticket has no confirmed bug link yet", acmeTicket && acmeTicket.confirmed_bug_links.length === 0);
check("Scenario A: Acme has no confirmed BUG-231 ticket at all", !csvConfirmedCustomers.has("acme"));
check("Scenario A: BUG-231 affects csv-export, introduced in 2.3.0, fixed in 2.3.2", csvExportBug && csvExportBug.feature_refs.includes("csv-export") && bugReleaseEvidence.get(csvExportBugId).introducedIn === "2.3.0" && bugReleaseEvidence.get(csvExportBugId).fixedIn === "2.3.2" && csvExportBug.status === "fixed");
check("Scenario A: release 2.3.2 notes document the BUG-231 fix", releasesByVersion.get("2.3.2")?.notes.includes(csvExportBugId));
check("Scenario A: every curated BUG-231 ticket is still confirmed", curatedCsvExportConfirmedTickets.every((ticketKey) => csvConfirmedTickets.includes(ticketKey)), csvConfirmedTickets.join(", "));
check("Scenario A: BUG-231 has at least 15 confirmed tickets from at least 8 customers", csvConfirmedTickets.length >= 15 && csvConfirmedCustomers.size >= 8, `tickets=${csvConfirmedTickets.length} customers=${csvConfirmedCustomers.size}`);
check("Scenario A: every BUG-231 confirmed ticket concerns csv-export", csvConfirmedTickets.every((ticketKey) => ticketsByKey.get(ticketKey).feature_refs.includes("csv-export")));
check("Scenario A: KB-012 addresses BUG-231 and covers csv-export", articles.some((article) => article.source_id === "KB-012" && article.bug_refs.includes(csvExportBugId) && article.feature_refs.includes("csv-export")));
const unconfirmedCsvTickets = tickets.filter((ticket) => ticket.feature_refs.includes("csv-export") && ticket.confirmed_bug_links.length === 0 && !/^(Configuration|Not a defect)/.test(ticket.resolution ?? ""));
check("Scenario A: curated similar csv-export tickets remain unconfirmed", curatedUnconfirmedSimilarCsvTickets.every((ticketKey) => unconfirmedCsvTickets.some((ticket) => ticket.source_id === ticketKey)));
check("Scenario A: at least 8 similar csv-export tickets have no confirmed link", unconfirmedCsvTickets.length >= 8, `found ${unconfirmedCsvTickets.length}`);
check("Scenario A: at least one BUG-231 ticket is still unresolved although the fix release exists", csvConfirmedTickets.some((ticketKey) => !["resolved", "closed"].includes(ticketsByKey.get(ticketKey).status)));

const forbiddenTickets = tickets.filter((ticket) => forbiddenPattern.test(`${ticket.subject} ${ticket.description}`));
const forbiddenLinkedTickets = forbiddenTickets.filter((ticket) => ticket.confirmed_bug_links.length > 0);
const forbiddenUnlinkedTickets = forbiddenTickets.filter((ticket) => ticket.confirmed_bug_links.length === 0);
checkAll("Scenario B: 403 tickets link only to documented permission defects", forbiddenLinkedTickets, (ticket) => ticket.confirmed_bug_links.every((link) => permissionDefectBugs.has(normalizeBugRef(link.bug_ref))));
checkAll("Scenario B: unlinked 403 tickets are explained as configuration, not a defect, or left without a root cause", forbiddenUnlinkedTickets, (ticket) => ticket.resolution === null || unlinkedForbiddenResolutionPattern.test(ticket.resolution));
check("Scenario B: at least 30 403 tickets are configuration or entitlement problems", forbiddenUnlinkedTickets.filter((ticket) => /^(Configuration|Not a defect)/.test(ticket.resolution ?? "")).length >= 30, `found ${forbiddenUnlinkedTickets.length} unlinked`);
check("Scenario B: at least 10 403 tickets are confirmed product defects", forbiddenLinkedTickets.length >= 10, `found ${forbiddenLinkedTickets.length}`);

const webhookDelayTickets = tickets.filter((ticket) => ticket.feature_refs.includes("webhooks") && webhookDelayPattern.test(`${ticket.subject} ${ticket.description}`));
check("Scenario C: at least 25 webhook delay tickets exist", webhookDelayTickets.length >= 25, `found ${webhookDelayTickets.length}`);
checkAll("Scenario C: no webhook delay ticket has a confirmed bug link", webhookDelayTickets, (ticket) => ticket.confirmed_bug_links.length === 0);
check("Scenario C: BUG-247 has no confirmed ticket links", confirmedTicketsForBug(unconfirmedWebhookBugId).length === 0);
check("Scenario C: webhook delay tickets span at least four release lines", new Set(webhookDelayTickets.map((ticket) => normalizeVersion(ticket.reported_version)).filter((parsed) => parsed.kind === "exact").map((parsed) => parsed.version.split(".").slice(0, 2).join("."))).size >= 4);

const unexpectedAnomalies = [...observedAnomalies].filter((anomaly) => !expectedAnomalies.has(anomaly));
const missingAnomalies = [...expectedAnomalies].filter((anomaly) => !observedAnomalies.has(anomaly));
check("only documented data-quality anomalies are present", unexpectedAnomalies.length === 0, unexpectedAnomalies.join(", "));
check("every documented data-quality anomaly is present", missingAnomalies.length === 0, missingAnomalies.join(", "));

const textOnlyBugMentions = tickets.flatMap((ticket) => {
  const linked = new Set(ticket.confirmed_bug_links.map((link) => normalizeBugRef(link.bug_ref)));
  const mentioned = [...`${ticket.internal_notes ?? ""} ${ticket.resolution ?? ""}`.matchAll(/BUG-\d+/g)].map((match) => match[0]);
  return [...new Set(mentioned)].filter((bugKey) => !linked.has(bugKey)).map((bugKey) => `${ticket.source_id}->${bugKey}`);
});

const normalizationVariants = {
  customerRefsNeedingNormalization: tickets.filter((ticket) => ticket.customer_ref !== normalizeCustomerRef(ticket.customer_ref)).length,
  bugRefsNeedingNormalization: tickets.flatMap((ticket) => ticket.confirmed_bug_links).filter((link) => link.bug_ref !== normalizeBugRef(link.bug_ref)).length,
  ticketVersionsWithPrefixOrWhitespace: tickets.filter((ticket) => ticket.reported_version && /^\s|\s$|^v/i.test(ticket.reported_version)).length,
  ticketVersionsTwoPart: tickets.filter((ticket) => normalizeVersion(ticket.reported_version).kind === "minor-line").length,
  ticketVersionsMissing: tickets.filter((ticket) => ticket.reported_version === null).length,
};

const failures = results.filter((result) => !result.passed);
for (const result of results) {
  console.log(`${result.passed ? "PASS" : "FAIL"}  ${result.description}${result.detail && !result.passed ? `  [${result.detail}]` : ""}`);
}
console.log("");
console.log(`Documented anomalies observed: ${[...observedAnomalies].sort().join(", ") || "none"}`);
console.log(`Normalization variants: ${Object.entries(normalizationVariants).map(([name, count]) => `${name}=${count}`).join(" ")}`);
console.log(`Bug IDs mentioned only in ticket text (must not become CONFIRMED_AS): ${textOnlyBugMentions.length} mentions, e.g. ${textOnlyBugMentions.slice(0, 6).join(", ")}`);
console.log(`Scenario sizes: BUG-231 confirmed=${csvConfirmedTickets.length} customers=${csvConfirmedCustomers.size}; unconfirmed csv=${unconfirmedCsvTickets.length}; 403 linked=${forbiddenLinkedTickets.length} unlinked=${forbiddenUnlinkedTickets.length}; webhook delays=${webhookDelayTickets.length}`);
console.log("");
const seedFiles = [
  ...["customers.csv", "features.csv", "releases.csv", "tickets.json", "bugs.json"].map((fileName) => join(seedDirectory, fileName)),
  ...readdirSync(knowledgeDirectory).sort().map((fileName) => join(knowledgeDirectory, fileName)),
];
const combinedHash = createHash("sha256");
for (const path of seedFiles) combinedHash.update(`${relative(seedDirectory, path).replaceAll("\\", "/")}:${sha256(path)}\n`);
for (const path of seedFiles.slice(0, 5)) console.log(`sha256  ${sha256(path)}  ${relative(repositoryRoot, path).replaceAll("\\", "/")}`);
console.log(`sha256  ${combinedHash.digest("hex")}  data/seed (all ${seedFiles.length} files)`);
console.log("");
console.log(`${results.length - failures.length}/${results.length} checks passed`);
process.exitCode = failures.length === 0 ? 0 : 1;
