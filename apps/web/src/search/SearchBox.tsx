import { keepPreviousData } from '@tanstack/react-query'
import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import type { SearchMatch } from '../api/generated/model'
import { useSearch } from '../api/generated/taxonomy'
import { formatCount } from '../lib/format'
import { useDebouncedValue } from '../lib/useDebouncedValue'
import { ancestorTrail, highlight } from './text'
import styles from './SearchBox.module.css'

const MIN_QUERY_LENGTH = 2
const DEBOUNCE_MS = 200
const RESULT_LIMIT = 20

type SearchBoxProps = {
  onSelect: (id: number) => void
}

export function SearchBox({ onSelect }: SearchBoxProps) {
  const [text, setText] = useState('')
  const [isOpen, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const listId = useId()

  const typed = text.trim()
  const query = useDebouncedValue(typed, DEBOUNCE_MS)
  const isQueryable = query.length >= MIN_QUERY_LENGTH

  const { data, isFetching, isError, isPlaceholderData } = useSearch(
    { q: query, limit: RESULT_LIMIT },
    { query: { enabled: isQueryable, placeholderData: keepPreviousData } },
  )

  const matches = isQueryable ? (data ?? []) : []
  const activeIndex = Math.min(active, matches.length - 1)
  const activeMatch = matches[activeIndex]
  const isListShown = isOpen && typed.length >= MIN_QUERY_LENGTH
  const isStale = isPlaceholderData || typed !== query

  useEffect(() => {
    const focusOnSlash = (event: globalThis.KeyboardEvent) => {
      const target = event.target as HTMLElement
      if (event.key !== '/' || target.closest('input, textarea, [contenteditable]')) return
      event.preventDefault()
      inputRef.current?.focus()
    }
    document.addEventListener('keydown', focusOnSlash)
    return () => document.removeEventListener('keydown', focusOnSlash)
  }, [])

  const choose = (match: SearchMatch) => {
    onSelect(match.id)
    setOpen(false)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp': {
        event.preventDefault()
        setOpen(true)
        const step = event.key === 'ArrowDown' ? 1 : -1
        setActive(Math.max(0, Math.min(activeIndex + step, matches.length - 1)))
        break
      }
      case 'Enter':
        if (isListShown && activeMatch) {
          event.preventDefault()
          choose(activeMatch)
        }
        break
      case 'Escape':
        if (isListShown) setOpen(false)
        else setText('')
        break
    }
  }

  return (
    <div className={styles.search}>
      <input
        ref={inputRef}
        type="search"
        role="combobox"
        aria-label="Search categories"
        aria-expanded={isListShown}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={
          isListShown && activeMatch ? `${listId}-${activeMatch.id}` : undefined
        }
        placeholder="Search categories"
        autoComplete="off"
        spellCheck={false}
        className={styles.input}
        value={text}
        onChange={(event) => {
          setText(event.target.value)
          setActive(0)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={onKeyDown}
      />
      <kbd className={styles.shortcut} aria-hidden="true">
        /
      </kbd>

      {isListShown && (
        <div className={styles.popover}>
          <ul
            id={listId}
            role="listbox"
            aria-label="Search results"
            aria-busy={isFetching}
            data-stale={isStale && matches.length > 0}
            className={styles.results}
          >
            {matches.map((match, i) => (
              <li
                key={match.id}
                id={`${listId}-${match.id}`}
                role="option"
                aria-label={match.label}
                aria-describedby={`${listId}-${match.id}-trail`}
                aria-selected={i === activeIndex}
                className={styles.result}
                onMouseDown={(event) => event.preventDefault()}
                onMouseMove={() => setActive(i)}
                onClick={() => choose(match)}
              >
                <span className={styles.label}>
                  {highlight(match.label, query).map((segment, j) =>
                    segment.match ? <mark key={j}>{segment.text}</mark> : segment.text,
                  )}
                </span>
                {match.size > 0 && <span className={styles.count}>{formatCount(match.size)}</span>}
                <span id={`${listId}-${match.id}-trail`} className={styles.trail}>
                  {ancestorTrail(match.path)}
                </span>
              </li>
            ))}
          </ul>
          <SearchStatus
            query={query}
            isWaiting={isStale || isFetching}
            isError={isError}
            isEmpty={matches.length === 0}
          />
        </div>
      )}
    </div>
  )
}

function SearchStatus({
  query,
  isWaiting,
  isError,
  isEmpty,
}: {
  query: string
  isWaiting: boolean
  isError: boolean
  isEmpty: boolean
}) {
  if (isError) return <p className={styles.status}>Search failed. Try again.</p>
  if (!isEmpty) return null
  if (isWaiting) return <p className={styles.status}>Searching…</p>
  return <p className={styles.status}>No categories match “{query}”.</p>
}
