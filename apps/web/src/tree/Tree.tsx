import { useVirtualizer } from '@tanstack/react-virtual'
import { useCallback, useEffect, useReducer, useRef } from 'react'
import { useGetNode } from '../api/generated/taxonomy'
import {
  ROOT_ID,
  rowIndexOf,
  rowKey,
  treeReducer,
  type Expansion,
  type ExpandedNodes,
} from './treeModel'
import { TreeItem } from './TreeItem'
import { useRowNodes, useTreeRows } from './useTreeRows'
import styles from './Tree.module.css'

const ROW_HEIGHT = 28

type TreeProps = {
  selectedId: number | null
  onSelect: (id: number) => void
}

const noneExpanded: ExpandedNodes = new Map()

export function Tree({ selectedId, onSelect }: TreeProps) {
  const [expanded, dispatch] = useReducer(treeReducer, noneExpanded)
  const { rows, isPending, error } = useTreeRows(expanded)
  const viewportRef = useRef<HTMLDivElement>(null)

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => viewportRef.current,
    estimateSize: () => ROW_HEIGHT,
    getItemKey: (i) => rowKey(rows[i]!.parentId, rows[i]!.index),
    overscan: 10,
  })
  const items = virtualizer.getVirtualItems()
  const nodeAt = useRowNodes(rows, items[0]?.index ?? 0, items.at(-1)?.index ?? -1)

  const { data: selected } = useGetNode(selectedId ?? 0, {
    query: { enabled: selectedId !== null },
  })

  useEffect(() => {
    if (selected) dispatch({ type: 'reveal', node: selected })
  }, [selected])

  const scrolledTo = useRef<number | null>(null)
  useEffect(() => {
    if (!selected || scrolledTo.current === selected.id) return
    const parentId = selected.ancestors.at(-1)?.id ?? ROOT_ID
    const index = rowIndexOf(rows, parentId, selected.index)
    if (index >= 0) {
      virtualizer.scrollToIndex(index, { align: 'auto' })
      scrolledTo.current = selected.id
    }
  }, [selected, rows, virtualizer])

  const toggle = useCallback(
    (id: number, expansion: Expansion) => dispatch({ type: 'toggle', id, expansion }),
    [],
  )

  if (error) return <p className={styles.message}>Couldn’t load the taxonomy.</p>
  if (isPending) return <p className={styles.message}>Loading…</p>

  return (
    <div ref={viewportRef} className={styles.viewport}>
      <div
        role="tree"
        aria-label="Categories"
        className={styles.tree}
        style={{ height: virtualizer.getTotalSize() }}
      >
        {items.map((item) => {
          const row = rows[item.index]!
          const node = nodeAt(row)
          return (
            <TreeItem
              key={item.key}
              row={row}
              offset={item.start}
              node={node}
              isExpanded={node ? expanded.has(node.id) : false}
              isSelected={node?.id === selectedId}
              onToggle={toggle}
              onSelect={onSelect}
            />
          )
        })}
      </div>
    </div>
  )
}
