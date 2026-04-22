import { NextRequest, NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

const MESSAGE_TYPE_ENDPOINTS: Record<string, string> = {
  'process-order': '/process-order',
  'rebuild-index': '/rebuild-index',
  'some-query': '/some-query',
};

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const messageType = searchParams.get('messageType');
  const messageCount = searchParams.get('messageCount') ?? '1';

  if (!messageType || !(messageType in MESSAGE_TYPE_ENDPOINTS)) {
    return NextResponse.json({ error: 'Invalid messageType' }, { status: 400 });
  }

  const endpoint = MESSAGE_TYPE_ENDPOINTS[messageType];
  const upstream = `${getDashboardApiUrl()}${endpoint}?messageCount=${messageCount}`;
  try {
    const response = await fetch(upstream, { cache: 'no-store' });
    return new NextResponse(null, { status: response.status });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(`[/api/submit proxy] Failed to reach ${upstream}:`, message);
    return NextResponse.json({ error: `Could not reach Dashboard API: ${message}` }, { status: 502 });
  }
}
