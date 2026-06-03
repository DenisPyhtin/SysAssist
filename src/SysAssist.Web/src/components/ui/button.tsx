import { cva, type VariantProps } from 'class-variance-authority'
import type { ButtonHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'

const buttonVariants = cva(
  'inline-flex h-10 items-center justify-center gap-2 rounded-md px-3 text-sm font-medium transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        primary: 'border border-[#d6b889]/45 bg-[#d6b889] text-[#1d130d] shadow-[0_8px_18px_rgba(110,75,47,0.18)] hover:border-[#ead4ad]/70 hover:bg-[#ead4ad] focus-visible:outline-[#d6b889]',
        ghost: 'border border-[#695241]/70 bg-[#241913]/65 text-[#eadfce] hover:border-[#b8996f]/55 hover:bg-[#302219] focus-visible:outline-[#b8996f]',
      },
    },
    defaultVariants: {
      variant: 'ghost',
    },
  },
)

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> &
  VariantProps<typeof buttonVariants>

export function Button({ className, variant, ...props }: ButtonProps) {
  return <button className={cn(buttonVariants({ variant }), className)} {...props} />
}
