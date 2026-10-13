import { type ReactNode, useId } from 'react'

interface PanelProps {
  title: string
  children: ReactNode
}

/** A titled white card. The heading labels the section for screen readers. */
export function Panel({ title, children }: PanelProps) {
  const headingId = useId()
  return (
    <section aria-labelledby={headingId} className="rounded-lg border border-line bg-white p-5">
      <h2 id={headingId} className="mb-4 text-base">
        {title}
      </h2>
      {children}
    </section>
  )
}

interface PageHeaderProps {
  title: string
  children?: ReactNode
}

export function PageHeader({ title, children }: PageHeaderProps) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl sm:text-3xl">{title}</h1>
      {children && <p className="mt-1 max-w-2xl text-sm text-muted">{children}</p>}
    </div>
  )
}
