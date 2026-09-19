import { describe, expect, it } from 'vitest'
import { ancestorTrail, highlight } from './text'

describe('highlight', () => {
  it('marks every case-insensitive occurrence', () => {
    expect(highlight('Hot dog, hotdog', 'dog')).toEqual([
      { text: 'Hot ', match: false },
      { text: 'dog', match: true },
      { text: ', hot', match: false },
      { text: 'dog', match: true },
    ])
  })

  it('keeps the original casing of the text', () => {
    expect(highlight('Dogwood', 'DOG')).toEqual([
      { text: 'Dog', match: true },
      { text: 'wood', match: false },
    ])
  })

  it('returns the text untouched when there is nothing to match', () => {
    expect(highlight('fern', '  ')).toEqual([{ text: 'fern', match: false }])
    expect(highlight('fern', 'moss')).toEqual([{ text: 'fern', match: false }])
  })
})

describe('ancestorTrail', () => {
  it('drops the root and the node itself and keeps the first synonym', () => {
    expect(ancestorTrail('ImageNet > plant, flora > moss')).toBe('plant')
  })

  it('shortens long paths from the front', () => {
    expect(ancestorTrail('root > a > b > c > d > leaf')).toBe('… › b › c › d')
  })

  it('is empty for top-level nodes', () => {
    expect(ancestorTrail('root')).toBe('')
    expect(ancestorTrail('root > child')).toBe('')
  })
})
