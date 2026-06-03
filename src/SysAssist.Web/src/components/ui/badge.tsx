import { cn } from '../../lib/utils'
import type { ReactNode } from 'react'

type BadgeProps = {
  children: ReactNode
  tone?: 'neutral' | 'warning' | 'danger' | 'success'
}

const toneMap = {
  neutral: 'border-[#6d5948] bg-[#2b2019] text-[#e7dac8]',
  warning: 'border-[#b8996f]/70 bg-[#6b4a2e]/24 text-[#e8c99d]',
  danger: 'border-[#b98278]/70 bg-[#6e332e]/22 text-[#edc3bd]',
  success: 'border-[#8fa98a]/70 bg-[#3d573c]/22 text-[#d7e4cf]',
}

export function Badge({ children, tone = 'neutral' }: BadgeProps) {
  return (
    <span className={cn('inline-flex rounded-md border px-2 py-1 text-xs font-medium', toneMap[tone])}>
      {children}
    </span>
  )
}
