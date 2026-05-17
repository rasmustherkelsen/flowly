import { NextRequest, NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const upstream = `${getDashboardApiUrl()}/run-recurring-job?${searchParams.toString()}`;
  try {
    const response = await fetch(upstream, { cache: 'no-store' });
    return new NextResponse(null, { status: response.status });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`[/api/run-recurring-job proxy] Failed to reach ${upstream}:`, message);
    return NextResponse.json({ error: `Could not reach Dashboard API: ${message}` }, { status: 502 });
  }
}
