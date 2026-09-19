import { useCallback, useEffect, useReducer, useRef } from 'react'
import { useGetNode } from '../api/generated/taxonomy'
import { treeReducer, type Expansion, type ExpandedNodes } from './treeModel'
import { TreeItem } from './TreeItem'
import { useTreeRows } from './useTreeRows'
import styles from './Tree.module.css'

type TreeProps = {
  selectedId: number | null
  onSelect: (id: number) => void
}

const noneExpanded: ExpandedNodes = new Map()

export function Tree({ selectedId, onSelect }: TreeProps) {
  const [expanded, dispatch] = useReducer(treeReducer, noneExpanded)
  const { rows, nodeAt, isPending, error } = useTreeRows(expanded)
  const listRef = useRef<HTMLDivElement>(null)
  const scrolledTo = useRef<number | null>(null)

  const { data: selected } = useGetNode(selectedId ?? 0, {
    query: { enabled: selectedId !== null },
  })

  useEffect(() => {
    if (selected) dispatch({ type: 'reveal', node: selected })
  }, [selected])

  useEffect(() => {
    if (!selected || scrolledTo.current === selected.id) return
    const element = listRef.current?.querySelector(`[data-node-id="${selected.id}"]`)
    if (element) {
      element.scrollIntoView({ block: 'nearest' })
      scrolledTo.current = selected.id
    }
  })

  const toggle = useCallback(
    (id: number, expansion: Expansion) => dispatch({ type: 'toggle', id, expansion }),
    [],
  )

  if (error) return <p className={styles.message}>Couldn’t load the taxonomy.</p>
  if (isPending) return <p className={styles.message}>Loading…</p>

  return (
    <div ref={listRef} role="tree" aria-label="Categories" className={styles.tree}>
      {rows.map((row) => {
        const node = nodeAt(row)
        return (
          <TreeItem
            key={`${row.parentId}:${row.index}`}
            row={row}
            node={node}
            isExpanded={node ? expanded.has(node.id) : false}
            isSelected={node?.id === selectedId}
            onToggle={toggle}
            onSelect={onSelect}
          />
        )
      })}
    </div>
  )
}
