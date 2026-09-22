const contactEmail = 'boros.csaba94@gmail.com'

export function mailto(subject: string, body = '') {
  return `mailto:${contactEmail}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
}
