import { splitLabel } from '../lib/format'

export type Segment = { text: string; match: boolean }

export function highlight(text: string, query: string): Segment[] {
  const needle = query.trim().toLowerCase()
  if (!needle) return [{ text, match: false }]

  const haystack = text.toLowerCase()
  const segments: Segment[] = []
  let from = 0

  for (let at = haystack.indexOf(needle); at !== -1; at = haystack.indexOf(needle, from)) {
    if (at > from) segments.push({ text: text.slice(from, at), match: false })
    segments.push({ text: text.slice(at, at + needle.length), match: true })
    from = at + needle.length
  }

  if (from < text.length) segments.push({ text: text.slice(from), match: false })
  return segments
}

const TRAIL_LENGTH = 3

export function ancestorTrail(path: string) {
  const ancestors = path
    .split(' > ')
    .slice(1, -1)
    .map((label) => splitLabel(label)[0])
  const shown = ancestors.slice(-TRAIL_LENGTH)
  return [...(ancestors.length > shown.length ? ['…'] : []), ...shown].join(' › ')
}
