const steps = ['Access', 'Connect AWS', 'Verification', 'Audit', 'Findings'] as const

export function Stepper({ active }: { active: (typeof steps)[number] }) {
  const activeIndex = steps.indexOf(active)

  return (
    <div className="flow">
      {steps.map((step, index) => (
        <div key={step} className={index === activeIndex ? 'step active' : 'step'}>
          {step}
          {index < activeIndex && ' ✓'}
        </div>
      ))}
    </div>
  )
}
