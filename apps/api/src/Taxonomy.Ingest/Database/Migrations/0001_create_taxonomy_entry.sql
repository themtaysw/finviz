CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- One row per category in pre-order. `name` and `size` are the linear form; the remaining columns
-- are derived from them at load time.
CREATE TABLE taxonomy_entry
(
    id        integer  PRIMARY KEY,
    parent_id integer  REFERENCES taxonomy_entry (id),
    depth     smallint NOT NULL CHECK (depth >= 0),
    label     text     NOT NULL,
    name      text     NOT NULL,
    size      integer  NOT NULL CHECK (size >= 0)
);

-- Children of a node: its subtree is the id range (id, id + size], one level deeper.
CREATE INDEX taxonomy_entry_depth_id_idx ON taxonomy_entry (depth, id) INCLUDE (label, size);

CREATE INDEX taxonomy_entry_parent_id_idx ON taxonomy_entry (parent_id);

CREATE INDEX taxonomy_entry_label_trgm_idx ON taxonomy_entry USING gin (label gin_trgm_ops);
