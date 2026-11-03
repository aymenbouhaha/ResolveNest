// ResolveNest graph schema: uniqueness constraints on the canonical `id` of each node label.
// Neo4j Community Edition compatible. Idempotent: safe to run repeatedly.
// Each uniqueness constraint creates its own backing RANGE index, so no separate index on `id` is needed.
// Documentation: docs/graph-schema.md

CREATE CONSTRAINT customer_id_unique IF NOT EXISTS
FOR (n:Customer) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT support_ticket_id_unique IF NOT EXISTS
FOR (n:SupportTicket) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT bug_id_unique IF NOT EXISTS
FOR (n:Bug) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT feature_id_unique IF NOT EXISTS
FOR (n:Feature) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT release_id_unique IF NOT EXISTS
FOR (n:Release) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT knowledge_article_id_unique IF NOT EXISTS
FOR (n:KnowledgeArticle) REQUIRE n.id IS UNIQUE;

CREATE CONSTRAINT reply_draft_id_unique IF NOT EXISTS
FOR (n:ReplyDraft) REQUIRE n.id IS UNIQUE;
