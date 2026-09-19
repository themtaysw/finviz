import { useCallback, useSyncExternalStore } from 'react'

const PARAM = 'node'

function subscribe(onChange: () => void) {
  window.addEventListener('popstate', onChange)
  return () => window.removeEventListener('popstate', onChange)
}

const getSearch = () => window.location.search

function parseId(search: string) {
  const value = Number(new URLSearchParams(search).get(PARAM))
  return Number.isInteger(value) && value > 0 ? value : null
}

export function useSelectedNodeId() {
  const selectedId = parseId(useSyncExternalStore(subscribe, getSearch))

  const select = useCallback((id: number | null) => {
    const url = new URL(window.location.href)
    if (id === null) url.searchParams.delete(PARAM)
    else url.searchParams.set(PARAM, String(id))

    if (url.href === window.location.href) return
    window.history.pushState(null, '', url)
    window.dispatchEvent(new PopStateEvent('popstate'))
  }, [])

  return [selectedId, select] as const
}
