import { NextRequest, NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const upstream = `${getDashboardApiUrl()}/api/dead-letters?${searchParams.toString()}`;
  try {
    const response = await fetch(upstream, { cache: 'no-store' });
    const data = await response.json();
    return NextResponse.json(data, { status: response.status });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`[/api/dead-letters proxy] Failed to reach ${upstream}:`, message);
    return NextResponse.json({ error: `Could not reach Dashboard API: ${message}` }, { status: 502 });
  }
}
