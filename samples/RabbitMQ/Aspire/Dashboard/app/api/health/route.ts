import { NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

export async function GET() {
  const resolvedUrl = getDashboardApiUrl();

  // Surface all Aspire service discovery env vars so the URL resolution can be debugged
  const serviceVars = Object.entries(process.env)
    .filter(([k]) => k.toLowerCase().startsWith('services__'))
    .reduce<Record<string, string>>((acc, [k, v]) => { acc[k] = v ?? ''; return acc; }, {});

  let upstreamStatus: number | string = 'not checked';
  try {
    const res = await fetch(`${resolvedUrl}/api/jobs?page=1&pageSize=1`, { cache: 'no-store' });
    upstreamStatus = res.status;
  } catch (e) {
    upstreamStatus = e instanceof Error ? e.message : String(e);
  }

  return NextResponse.json({ resolvedUrl, upstreamStatus, serviceVars });
}
