import { Component, type ReactNode } from 'react'
import Markdown, { type Components, type UrlTransform } from 'react-markdown'
import remarkGfm from 'remark-gfm'

const ALLOWED_ELEMENTS = [
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
  'p', 'br', 'hr', 'strong', 'em', 'del',
  'ul', 'ol', 'li',
  'blockquote', 'code', 'pre', 'a',
  'table', 'thead', 'tbody', 'tr', 'th', 'td',
]

const SAFE_LINK_PROTOCOLS = /^(https?:|mailto:)/i

const allowOnlySafeLinks: UrlTransform = (url, key) =>
  key === 'href' && SAFE_LINK_PROTOCOLS.test(url.trim()) ? url : ''

const GUIDE_COMPONENTS: Components = {
  h1: ({ children }) => <h4 className="mt-1 mb-2 text-sm font-semibold text-foreground">{children}</h4>,
  h2: ({ children }) => <h5 className="mt-4 mb-1.5 text-sm font-semibold text-foreground">{children}</h5>,
  h3: ({ children }) => <h6 className="mt-3 mb-1 text-sm font-medium text-foreground">{children}</h6>,
  h4: ({ children }) => <h6 className="mt-3 mb-1 text-sm font-medium text-foreground">{children}</h6>,
  h5: ({ children }) => <h6 className="mt-2 mb-1 text-sm font-medium text-foreground">{children}</h6>,
  h6: ({ children }) => <h6 className="mt-2 mb-1 text-sm font-medium text-foreground">{children}</h6>,
  p: ({ children }) => <p className="my-2 leading-relaxed">{children}</p>,
  ul: ({ children }) => <ul className="my-2 list-disc space-y-1 pl-5 marker:text-muted-foreground">{children}</ul>,
  ol: ({ children }) => <ol className="my-2 list-decimal space-y-1 pl-5 marker:text-muted-foreground">{children}</ol>,
  li: ({ children }) => <li className="pl-1">{children}</li>,
  blockquote: ({ children }) => (
    <blockquote className="my-2 border-l-2 border-border pl-3 text-muted-foreground">{children}</blockquote>
  ),
  code: ({ children }) => (
    <code className="rounded bg-background px-1 py-0.5 font-mono text-[0.8em] break-words ring-1 ring-border">
      {children}
    </code>
  ),
  pre: ({ children }) => (
    <pre className="my-2 overflow-x-auto rounded-md bg-background p-3 text-xs ring-1 ring-border [&_code]:bg-transparent [&_code]:p-0 [&_code]:ring-0">
      {children}
    </pre>
  ),
  table: ({ children }) => (
    <div className="my-2 max-w-full overflow-x-auto rounded-md ring-1 ring-border">
      <table className="w-full border-collapse text-left text-xs">{children}</table>
    </div>
  ),
  th: ({ children }) => <th className="border-b bg-muted/60 px-2 py-1.5 font-medium">{children}</th>,
  td: ({ children }) => <td className="border-b px-2 py-1.5 align-top">{children}</td>,
  a: ({ href, children }) =>
    href ? (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer nofollow"
        className="text-primary underline underline-offset-2 hover:text-primary/80"
      >
        {children}
      </a>
    ) : (
      <span>{children}</span>
    ),
}

interface GuideMarkdownProps {
  content: string
}

export function GuideMarkdown({ content }: GuideMarkdownProps) {
  const plainText = <pre className="font-sans text-xs leading-relaxed whitespace-pre-wrap break-words">{content}</pre>

  return (
    <MarkdownErrorBoundary fallback={plainText}>
      <div className="text-sm text-foreground/90">
        <Markdown
          remarkPlugins={[remarkGfm]}
          allowedElements={ALLOWED_ELEMENTS}
          unwrapDisallowed
          skipHtml
          urlTransform={allowOnlySafeLinks}
          components={GUIDE_COMPONENTS}
        >
          {content}
        </Markdown>
      </div>
    </MarkdownErrorBoundary>
  )
}

interface MarkdownErrorBoundaryProps {
  fallback: ReactNode
  children: ReactNode
}

class MarkdownErrorBoundary extends Component<MarkdownErrorBoundaryProps, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children
  }
}
