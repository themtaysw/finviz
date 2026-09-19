import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './server'

const scrollTops = new WeakMap<Element, number>()

Object.defineProperties(HTMLElement.prototype, {
  offsetHeight: { configurable: true, get: () => 600 },
  offsetWidth: { configurable: true, get: () => 400 },
  clientHeight: { configurable: true, get: () => 600 },
  scrollHeight: {
    configurable: true,
    get(this: HTMLElement) {
      const content = this.firstElementChild
      return content instanceof HTMLElement ? parseFloat(content.style.height) || 0 : 0
    },
  },
})

Object.defineProperty(Element.prototype, 'scrollTop', {
  configurable: true,
  get(this: Element) {
    return scrollTops.get(this) ?? 0
  },
  set(this: Element, value: number) {
    scrollTops.set(this, value)
  },
})

Element.prototype.scrollTo = function (this: Element, options?: ScrollToOptions | number) {
  this.scrollTop = typeof options === 'number' ? options : (options?.top ?? 0)
  this.dispatchEvent(new Event('scroll'))
} as Element['scrollTo']

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
  window.history.replaceState(null, '', '/')
})
afterAll(() => server.close())
