import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'danger'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  /** A smaller button, for use inside a table row. */
  small?: boolean
}

const VARIANT_CLASSES: Record<Variant, string> = {
  primary: 'bg-brand text-white hover:bg-brand-dark',
  secondary: 'border border-line bg-white text-ink hover:bg-paper',
  danger: 'bg-red-700 text-white hover:bg-red-800',
}

export function Button({ variant = 'primary', small = false, type = 'button', className = '', ...rest }: ButtonProps) {
  const size = small ? 'px-3 py-1' : 'px-4 py-2'
  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center rounded-md text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${size} ${VARIANT_CLASSES[variant]} ${className}`}
      {...rest}
    />
  )
}
