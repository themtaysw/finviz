-- Lets a client size a node's child list before fetching any of it.
ALTER TABLE taxonomy_entry ADD COLUMN child_count integer NOT NULL DEFAULT 0 CHECK (child_count >= 0);

DROP INDEX taxonomy_entry_depth_id_idx;
CREATE INDEX taxonomy_entry_depth_id_idx ON taxonomy_entry (depth, id) INCLUDE (label, size, child_count);
