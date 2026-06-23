export default function FlowlyIcon({ size = 28 }: { size?: number }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width={size} height={size} style={{ flexShrink: 0 }}>
      <defs>
        <linearGradient id="flowly-bg" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#2563EB" />
          <stop offset="100%" stopColor="#1E3A8A" />
        </linearGradient>
      </defs>
      <rect width="128" height="128" rx="22" ry="22" fill="url(#flowly-bg)" />
      <polyline points="22,40 44,64 22,88" stroke="white" strokeWidth="11" strokeLinecap="round" strokeLinejoin="round" fill="none" opacity="0.30" />
      <polyline points="48,40 70,64 48,88" stroke="white" strokeWidth="11" strokeLinecap="round" strokeLinejoin="round" fill="none" opacity="0.65" />
      <polyline points="74,40 96,64 74,88" stroke="white" strokeWidth="11" strokeLinecap="round" strokeLinejoin="round" fill="none" opacity="1.0" />
    </svg>
  );
}
