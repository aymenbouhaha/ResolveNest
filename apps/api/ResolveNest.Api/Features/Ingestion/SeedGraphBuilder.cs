using System.Globalization;
using System.Text.Json;
using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed class SeedGraphBuilder
{
    private static readonly HashSet<string> BugStatuses = ["open", "fixed", "wontfix"];
    private static readonly HashSet<string> TicketStatuses = ["open", "pending_customer", "resolved", "closed"];

    public const string DocumentedReferencesConfigurationLocation = $"configuration:{SeedIngestionSettings.SectionName}:DocumentedUnresolvedBugReferences";

    public SeedImportPlan Build(
        SeedSourceSet source,
        DateTimeOffset ingestedAt,
        IReadOnlyCollection<DocumentedUnresolvedBugReference> documentedUnresolvedBugReferences)
    {
        var context = new BuildContext(ToZonedDateTime(ingestedAt), NormalizeDocumentedReferences(documentedUnresolvedBugReferences));
        if (documentedUnresolvedBugReferences.Any(reference => string.IsNullOrWhiteSpace(reference.TicketId) || string.IsNullOrWhiteSpace(reference.BugRef) || string.IsNullOrWhiteSpace(reference.Reason)))
        {
            context.Error(DocumentedReferencesConfigurationLocation, "every documented unresolved reference needs TicketId, BugRef and Reason.");
        }

        var customerIdsByKey = BuildCustomers(source.Customers, context);
        var featureIdsByKey = BuildFeatures(source.Features, context);
        var releaseIdsByVersion = BuildReleases(source.Releases, context);
        var bugIdsByKey = BuildBugs(source.Bugs, featureIdsByKey, releaseIdsByVersion, context);
        var uniqueTicketCount = BuildTickets(source.Tickets, customerIdsByKey, featureIdsByKey, bugIdsByKey, releaseIdsByVersion, context);

        foreach (var (ticketKey, bugKey) in context.DocumentedUnresolvedReferences.Except(context.MatchedDocumentedReferences))
        {
            context.Warning(DocumentedReferencesConfigurationLocation, $"documented unresolved reference {ticketKey} -> {bugKey} was not found in the source; the entry may be obsolete.");
        }

        var sourceCounts = new SourceRecordCounts(
            source.Customers.Count,
            source.Features.Count,
            source.Releases.Count,
            source.Tickets.Count,
            uniqueTicketCount,
            source.Bugs.Count);

        return new SeedImportPlan(
            sourceCounts,
            context.Customers,
            context.Features,
            context.Releases,
            context.Bugs,
            context.Tickets,
            context.Reported,
            context.AboutFeature,
            context.ConfirmedAs,
            context.Affects,
            context.IntroducedIn,
            context.FixedIn,
            context.SourceSystems.Order(StringComparer.Ordinal).ToList(),
            context.Issues);
    }

    private static HashSet<(string TicketKey, string BugKey)> NormalizeDocumentedReferences(IReadOnlyCollection<DocumentedUnresolvedBugReference> references) =>
        references
            .Where(reference => !string.IsNullOrWhiteSpace(reference.TicketId) && !string.IsNullOrWhiteSpace(reference.BugRef))
            .Select(reference => (SourceNormalization.TicketKey(reference.TicketId), SourceNormalization.BugKey(reference.BugRef)))
            .ToHashSet();

    private static Dictionary<string, string> BuildCustomers(IReadOnlyList<CsvSourceRow> rows, BuildContext context)
    {
        var idsByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var location = row.Location.ToString();
            var id = context.Required(row.Value("id"), "id", location);
            var sourceSystem = context.Required(row.Value("source_system"), "source_system", location);
            var sourceId = context.Required(row.Value("source_id"), "source_id", location);
            var accountSlug = context.Required(row.Value("account_slug"), "account_slug", location);
            var name = context.Required(row.Value("name"), "name", location);
            if (id is null || sourceSystem is null || sourceId is null || accountSlug is null || name is null)
            {
                continue;
            }

            var customerKey = SourceNormalization.CustomerKey(accountSlug);
            if (customerKey != accountSlug)
            {
                context.Error(location, $"account_slug '{accountSlug}' must already be trimmed and lowercase.");
                continue;
            }

            if (!context.HasCanonicalId(id, SourceNormalization.CustomerId(customerKey), location) || !context.IsFirstOccurrence(idsByKey, customerKey, id, location))
            {
                continue;
            }

            var properties = context.NodeProvenance(id, sourceSystem, sourceId, location, row.Value("synthetic") == "true");
            properties["accountSlug"] = accountSlug;
            properties["name"] = name;
            properties["plan"] = row.Value("plan");
            properties["region"] = row.Value("region");
            properties["deployment"] = row.Value("deployment");
            properties["customerSince"] = context.OptionalDate(row.Value("customer_since"), "customer_since", location);
            context.Customers.Add(properties);
        }

        return idsByKey;
    }

    private static Dictionary<string, string> BuildFeatures(IReadOnlyList<CsvSourceRow> rows, BuildContext context)
    {
        var idsByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var location = row.Location.ToString();
            var id = context.Required(row.Value("id"), "id", location);
            var sourceSystem = context.Required(row.Value("source_system"), "source_system", location);
            var sourceId = context.Required(row.Value("source_id"), "source_id", location);
            var name = context.Required(row.Value("name"), "name", location);
            if (id is null || sourceSystem is null || sourceId is null || name is null)
            {
                continue;
            }

            var featureKey = SourceNormalization.FeatureKey(sourceId);
            if (!context.HasCanonicalId(id, SourceNormalization.FeatureId(featureKey), location) || !context.IsFirstOccurrence(idsByKey, featureKey, id, location))
            {
                continue;
            }

            var properties = context.NodeProvenance(id, sourceSystem, sourceId, location, row.Value("synthetic") == "true");
            properties["name"] = name;
            properties["area"] = row.Value("area");
            properties["description"] = row.Value("description");
            context.Features.Add(properties);
        }

        return idsByKey;
    }

    private static Dictionary<string, string> BuildReleases(IReadOnlyList<CsvSourceRow> rows, BuildContext context)
    {
        var idsByVersion = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var location = row.Location.ToString();
            var id = context.Required(row.Value("id"), "id", location);
            var sourceSystem = context.Required(row.Value("source_system"), "source_system", location);
            var sourceId = context.Required(row.Value("source_id"), "source_id", location);
            var rawVersion = context.Required(row.Value("version"), "version", location);
            var releasedAt = context.RequiredTimestamp(row.Value("released_at"), "released_at", location);
            if (id is null || sourceSystem is null || sourceId is null || rawVersion is null || releasedAt is null)
            {
                continue;
            }

            var version = SourceNormalization.Version(rawVersion);
            if (!version.IsExact || version.Normalized != rawVersion)
            {
                context.Error(location, $"release version '{rawVersion}' must be an exact major.minor.patch version.");
                continue;
            }

            if (!context.HasCanonicalId(id, SourceNormalization.ReleaseId(rawVersion), location) || !context.IsFirstOccurrence(idsByVersion, rawVersion, id, location))
            {
                continue;
            }

            var properties = context.NodeProvenance(id, sourceSystem, sourceId, location, row.Value("synthetic") == "true");
            properties["version"] = rawVersion;
            properties["releaseType"] = row.Value("release_type");
            properties["releasedAt"] = releasedAt;
            properties["notes"] = row.Value("notes");
            context.Releases.Add(properties);
        }

        return idsByVersion;
    }

    private static Dictionary<string, string> BuildBugs(
        IReadOnlyList<LocatedRecord<BugSource>> records,
        IReadOnlyDictionary<string, string> featureIdsByKey,
        IReadOnlyDictionary<string, string> releaseIdsByVersion,
        BuildContext context)
    {
        var idsByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (sourceLocation, bug, synthetic) in records)
        {
            var location = sourceLocation.ToString();
            var id = context.Required(bug.Id, "id", location);
            var sourceSystem = context.Required(bug.SourceSystem, "source_system", location);
            var sourceId = context.Required(bug.SourceId, "source_id", location);
            var title = context.Required(bug.Title, "title", location);
            var status = context.Required(bug.Status, "status", location);
            var createdAt = context.RequiredTimestamp(bug.CreatedAt, "created_at", location);
            var updatedAt = context.RequiredTimestamp(bug.UpdatedAt, "updated_at", location);
            if (id is null || sourceSystem is null || sourceId is null || title is null || status is null || createdAt is null || updatedAt is null)
            {
                continue;
            }

            if (!BugStatuses.Contains(status))
            {
                context.Error(location, $"status '{status}' is not one of {string.Join(", ", BugStatuses)}.");
                continue;
            }

            var bugKey = SourceNormalization.BugKey(sourceId);
            if (!context.HasCanonicalId(id, SourceNormalization.BugId(bugKey), location) || !context.IsFirstOccurrence(idsByKey, bugKey, id, location))
            {
                continue;
            }

            var featureRefs = bug.FeatureRefs ?? [];
            if (featureRefs.Count == 0)
            {
                context.Error(location, "feature_refs must reference at least one feature.");
            }

            var introducedRelease = ResolveBugRelease(bug.IntroducedIn, "introduced_in", releaseIdsByVersion, location, context);
            var fixedRelease = ResolveBugRelease(bug.FixedIn, "fixed_in", releaseIdsByVersion, location, context);
            if (introducedRelease is not null && fixedRelease is not null && SourceNormalization.CompareExactVersions(fixedRelease.Value.Version, introducedRelease.Value.Version) <= 0)
            {
                context.Error(location, $"fixed_in {fixedRelease.Value.Version} is not later than introduced_in {introducedRelease.Value.Version}.");
            }

            var properties = context.NodeProvenance(id, sourceSystem, sourceId, location, synthetic);
            properties["title"] = title;
            properties["description"] = bug.Description;
            properties["status"] = status;
            properties["severity"] = bug.Severity;
            properties["workaround"] = bug.Workaround;
            properties["sourceIntroducedIn"] = bug.IntroducedIn;
            properties["sourceFixedIn"] = bug.FixedIn;
            properties["introducedInEvidence"] = bug.IntroducedInEvidence;
            properties["fixEvidence"] = bug.FixEvidence;
            properties["createdAt"] = createdAt;
            properties["updatedAt"] = updatedAt;
            context.Bugs.Add(properties);

            foreach (var featureRef in featureRefs.Distinct(StringComparer.Ordinal))
            {
                var featureKey = SourceNormalization.FeatureKey(featureRef);
                if (!featureIdsByKey.TryGetValue(featureKey, out var featureId))
                {
                    context.Error(location, $"feature_ref '{featureRef}' does not match any feature.");
                    continue;
                }

                var relationshipProperties = context.RelationshipProvenance(sourceSystem, location);
                relationshipProperties["sourceFeatureRef"] = featureRef;
                context.Affects.Add(new GraphRelationship(id, featureId, relationshipProperties));
            }

            if (introducedRelease is not null)
            {
                var relationshipProperties = context.RelationshipProvenance(sourceSystem, location);
                relationshipProperties["sourceVersion"] = bug.IntroducedIn;
                relationshipProperties["evidence"] = bug.IntroducedInEvidence;
                context.IntroducedIn.Add(new GraphRelationship(id, introducedRelease.Value.Id, relationshipProperties));
            }

            if (fixedRelease is not null)
            {
                var relationshipProperties = context.RelationshipProvenance(sourceSystem, location);
                relationshipProperties["sourceVersion"] = bug.FixedIn;
                relationshipProperties["evidence"] = bug.FixEvidence;
                context.FixedIn.Add(new GraphRelationship(id, fixedRelease.Value.Id, relationshipProperties));
            }
        }

        return idsByKey;
    }

    private static (string Id, string Version)? ResolveBugRelease(
        string? rawVersion,
        string fieldName,
        IReadOnlyDictionary<string, string> releaseIdsByVersion,
        string location,
        BuildContext context)
    {
        var version = SourceNormalization.Version(rawVersion);
        switch (version.Precision)
        {
            case VersionPrecision.Missing:
                return null;
            case VersionPrecision.Patch when releaseIdsByVersion.TryGetValue(version.Normalized!, out var releaseId):
                return (releaseId, version.Normalized!);
            case VersionPrecision.Patch:
                context.Error(location, $"{fieldName} '{rawVersion}' does not match any release.");
                return null;
            default:
                context.Warning(location, $"{fieldName} '{rawVersion}' is not an exact release version; no relationship created.");
                return null;
        }
    }

    private static int BuildTickets(
        IReadOnlyList<LocatedRecord<TicketSource>> records,
        IReadOnlyDictionary<string, string> customerIdsByKey,
        IReadOnlyDictionary<string, string> featureIdsByKey,
        IReadOnlyDictionary<string, string> bugIdsByKey,
        IReadOnlyDictionary<string, string> releaseIdsByVersion,
        BuildContext context)
    {
        var latestRecords = SelectLatestTicketRecords(records, context);
        foreach (var (sourceLocation, ticket, synthetic) in latestRecords)
        {
            BuildTicket(sourceLocation.ToString(), ticket, synthetic, customerIdsByKey, featureIdsByKey, bugIdsByKey, releaseIdsByVersion, context);
        }

        return latestRecords.Count;
    }

    private static List<LocatedRecord<TicketSource>> SelectLatestTicketRecords(IReadOnlyList<LocatedRecord<TicketSource>> records, BuildContext context)
    {
        var latestByKey = new Dictionary<string, (LocatedRecord<TicketSource> Record, DateTimeOffset UpdatedAt)>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var record in records)
        {
            var location = record.Location.ToString();
            var sourceId = context.Required(record.Record.SourceId, "source_id", location);
            var updatedAt = context.RequiredInstant(record.Record.UpdatedAt, "updated_at", location);
            if (sourceId is null || updatedAt is null)
            {
                continue;
            }

            var ticketKey = SourceNormalization.TicketKey(sourceId);
            if (!latestByKey.TryGetValue(ticketKey, out var existing))
            {
                latestByKey[ticketKey] = (record, updatedAt.Value);
                order.Add(ticketKey);
                continue;
            }

            if (updatedAt.Value == existing.UpdatedAt)
            {
                if (JsonSerializer.Serialize(record.Record) != JsonSerializer.Serialize(existing.Record.Record))
                {
                    context.Error(location, $"duplicate record for {ticketKey} has the same updated_at as {existing.Record.Location} but different content.");
                }

                continue;
            }

            var newer = updatedAt.Value > existing.UpdatedAt ? (record, updatedAt.Value) : existing;
            var older = updatedAt.Value > existing.UpdatedAt ? existing.Record : record;
            latestByKey[ticketKey] = newer;
            context.Warning(older.Location.ToString(), $"duplicate record for {ticketKey}; using {newer.Item1.Location} because it has the latest updated_at.");
        }

        return order.Select(key => latestByKey[key].Record).ToList();
    }

    private static void BuildTicket(
        string location,
        TicketSource ticket,
        bool synthetic,
        IReadOnlyDictionary<string, string> customerIdsByKey,
        IReadOnlyDictionary<string, string> featureIdsByKey,
        IReadOnlyDictionary<string, string> bugIdsByKey,
        IReadOnlyDictionary<string, string> releaseIdsByVersion,
        BuildContext context)
    {
        var id = context.Required(ticket.Id, "id", location);
        var sourceSystem = context.Required(ticket.SourceSystem, "source_system", location);
        var sourceId = context.Required(ticket.SourceId, "source_id", location);
        var customerRef = context.Required(ticket.CustomerRef, "customer_ref", location);
        var subject = context.Required(ticket.Subject, "subject", location);
        var status = context.Required(ticket.Status, "status", location);
        var createdAt = context.RequiredTimestamp(ticket.CreatedAt, "created_at", location);
        var updatedAt = context.RequiredTimestamp(ticket.UpdatedAt, "updated_at", location);
        if (id is null || sourceSystem is null || sourceId is null || customerRef is null || subject is null || status is null || createdAt is null || updatedAt is null)
        {
            return;
        }

        if (!TicketStatuses.Contains(status))
        {
            context.Error(location, $"status '{status}' is not one of {string.Join(", ", TicketStatuses)}.");
            return;
        }

        if (!context.HasCanonicalId(id, SourceNormalization.TicketId(SourceNormalization.TicketKey(sourceId)), location))
        {
            return;
        }

        var customerKey = SourceNormalization.CustomerKey(customerRef);
        if (!customerIdsByKey.TryGetValue(customerKey, out var customerId))
        {
            context.Error(location, $"customer_ref '{customerRef}' does not match any customer.");
            return;
        }

        var reportedVersion = SourceNormalization.Version(ticket.ReportedVersion);
        if (reportedVersion.Precision == VersionPrecision.Unparseable)
        {
            context.Warning(location, $"reported_version '{ticket.ReportedVersion}' cannot be normalized; stored as raw text only.");
        }
        else if (reportedVersion.IsExact && !releaseIdsByVersion.ContainsKey(reportedVersion.Normalized!))
        {
            context.Warning(location, $"reported_version '{ticket.ReportedVersion}' does not match a known release.");
        }

        var properties = context.NodeProvenance(id, sourceSystem, sourceId, location, synthetic);
        properties["subject"] = subject;
        properties["description"] = ticket.Description;
        properties["status"] = status;
        properties["priority"] = ticket.Priority;
        properties["channel"] = ticket.Channel;
        properties["reportedVersion"] = ticket.ReportedVersion;
        properties["reportedVersionNormalized"] = reportedVersion.Normalized;
        properties["reportedVersionPrecision"] = reportedVersion.PrecisionName;
        properties["internalNotes"] = ticket.InternalNotes;
        properties["resolution"] = ticket.Resolution;
        properties["sourceCustomerRef"] = customerRef;
        properties["createdAt"] = createdAt;
        properties["updatedAt"] = updatedAt;
        properties["resolvedAt"] = context.OptionalTimestamp(ticket.ResolvedAt, "resolved_at", location);
        context.Tickets.Add(properties);

        var reportedProperties = context.RelationshipProvenance(sourceSystem, location);
        reportedProperties["sourceCustomerRef"] = customerRef;
        context.Reported.Add(new GraphRelationship(customerId, id, reportedProperties));

        var featureRefs = ticket.FeatureRefs ?? [];
        if (featureRefs.Count == 0)
        {
            context.Warning(location, "ticket has no feature_refs; no ABOUT_FEATURE relationship created.");
        }

        var linkedFeatureIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var featureRef in featureRefs)
        {
            if (!featureIdsByKey.TryGetValue(SourceNormalization.FeatureKey(featureRef), out var featureId))
            {
                context.Error(location, $"feature_ref '{featureRef}' does not match any feature.");
                continue;
            }

            if (linkedFeatureIds.Add(featureId))
            {
                var relationshipProperties = context.RelationshipProvenance(sourceSystem, location);
                relationshipProperties["sourceFeatureRef"] = featureRef;
                context.AboutFeature.Add(new GraphRelationship(id, featureId, relationshipProperties));
            }
        }

        var ticketKey = SourceNormalization.TicketKey(sourceId);
        var unresolvedBugRefs = new List<string>();
        var linkedBugIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in ticket.ConfirmedBugLinks ?? [])
        {
            var bugRef = context.Required(link.BugRef, "confirmed_bug_links.bug_ref", location);
            var confirmedBy = context.Required(link.ConfirmedBy, "confirmed_bug_links.confirmed_by", location);
            var confirmedAt = context.RequiredTimestamp(link.ConfirmedAt, "confirmed_bug_links.confirmed_at", location);
            if (bugRef is null || confirmedBy is null || confirmedAt is null)
            {
                continue;
            }

            var bugKey = SourceNormalization.BugKey(bugRef);
            if (!bugIdsByKey.TryGetValue(bugKey, out var bugId))
            {
                if (context.DocumentedUnresolvedReferences.Contains((ticketKey, bugKey)))
                {
                    context.MatchedDocumentedReferences.Add((ticketKey, bugKey));
                    unresolvedBugRefs.Add(bugRef);
                    context.Warning(location, $"confirmed bug_ref '{bugRef}' is a documented unresolved reference; CONFIRMED_AS not created and the raw reference is kept in unresolvedBugRefs.");
                }
                else
                {
                    context.Error(location, $"confirmed bug_ref '{bugRef}' does not match any bug in the export and is not a documented unresolved reference.");
                }

                continue;
            }

            if (linkedBugIds.Add(bugId))
            {
                var relationshipProperties = context.RelationshipProvenance(sourceSystem, location);
                relationshipProperties["sourceBugRef"] = bugRef;
                relationshipProperties["confirmedBy"] = confirmedBy;
                relationshipProperties["confirmedAt"] = confirmedAt;
                context.ConfirmedAs.Add(new GraphRelationship(id, bugId, relationshipProperties));
            }
        }

        properties["unresolvedBugRefs"] = unresolvedBugRefs.Count > 0 ? unresolvedBugRefs : null;
    }

    private static ZonedDateTime ToZonedDateTime(DateTimeOffset value) => new(value.ToUniversalTime());

    private sealed class BuildContext(ZonedDateTime ingestedAt, HashSet<(string TicketKey, string BugKey)> documentedUnresolvedReferences)
    {
        public HashSet<(string TicketKey, string BugKey)> DocumentedUnresolvedReferences { get; } = documentedUnresolvedReferences;
        public HashSet<(string TicketKey, string BugKey)> MatchedDocumentedReferences { get; } = [];
        public List<IngestionIssue> Issues { get; } = [];
        public HashSet<string> SourceSystems { get; } = new(StringComparer.Ordinal);
        public List<IReadOnlyDictionary<string, object?>> Customers { get; } = [];
        public List<IReadOnlyDictionary<string, object?>> Features { get; } = [];
        public List<IReadOnlyDictionary<string, object?>> Releases { get; } = [];
        public List<IReadOnlyDictionary<string, object?>> Bugs { get; } = [];
        public List<IReadOnlyDictionary<string, object?>> Tickets { get; } = [];
        public List<GraphRelationship> Reported { get; } = [];
        public List<GraphRelationship> AboutFeature { get; } = [];
        public List<GraphRelationship> ConfirmedAs { get; } = [];
        public List<GraphRelationship> Affects { get; } = [];
        public List<GraphRelationship> IntroducedIn { get; } = [];
        public List<GraphRelationship> FixedIn { get; } = [];

        public void Error(string location, string message) => Issues.Add(new IngestionIssue(IngestionIssueSeverity.Error, location, message));

        public void Warning(string location, string message) => Issues.Add(new IngestionIssue(IngestionIssueSeverity.Warning, location, message));

        public string? Required(string? value, string fieldName, string location)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Error(location, $"required field '{fieldName}' is missing or empty.");
            return null;
        }

        public bool HasCanonicalId(string id, string expectedId, string location)
        {
            if (id == expectedId)
            {
                return true;
            }

            Error(location, $"id '{id}' does not match the canonical id '{expectedId}'.");
            return false;
        }

        public bool IsFirstOccurrence(Dictionary<string, string> idsByKey, string key, string id, string location)
        {
            if (idsByKey.TryAdd(key, id))
            {
                return true;
            }

            Error(location, $"duplicate canonical id '{id}'.");
            return false;
        }

        public DateTimeOffset? RequiredInstant(string? value, string fieldName, string location)
        {
            if (Required(value, fieldName, location) is null)
            {
                return null;
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }

            Error(location, $"'{fieldName}' value '{value}' is not an ISO 8601 timestamp.");
            return null;
        }

        public ZonedDateTime? RequiredTimestamp(string? value, string fieldName, string location) =>
            RequiredInstant(value, fieldName, location) is { } instant ? ToZonedDateTime(instant) : null;

        public ZonedDateTime? OptionalTimestamp(string? value, string fieldName, string location) =>
            string.IsNullOrWhiteSpace(value) ? null : RequiredTimestamp(value, fieldName, location);

        public LocalDate? OptionalDate(string? value, string fieldName, string location)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return new LocalDate(date.Year, date.Month, date.Day);
            }

            Error(location, $"'{fieldName}' value '{value}' is not a yyyy-MM-dd date.");
            return null;
        }

        public Dictionary<string, object?> NodeProvenance(string id, string sourceSystem, string sourceId, string location, bool synthetic)
        {
            SourceSystems.Add(sourceSystem);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["sourceSystem"] = sourceSystem,
                ["sourceId"] = sourceId,
                ["sourceRecord"] = location,
                ["ingestedAt"] = ingestedAt,
                ["ingestedBy"] = SeedGraphQueries.SeedOwner,
                ["synthetic"] = synthetic,
            };
        }

        public Dictionary<string, object?> RelationshipProvenance(string sourceSystem, string location) =>
            new(StringComparer.Ordinal)
            {
                ["sourceSystem"] = sourceSystem,
                ["sourceRecord"] = location,
                ["ingestedAt"] = ingestedAt,
                ["ingestedBy"] = SeedGraphQueries.SeedOwner,
            };
    }
}
