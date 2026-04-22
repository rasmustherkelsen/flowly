import { NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

export async function POST(
  _request: Request,
  { params }: { params: Promise<{ messageId: string }> }
) {
  const { messageId } = await params;
  const upstream = `${getDashboardApiUrl()}/api/dead-letters/${encodeURIComponent(messageId)}/requeue`;
  const response = await fetch(upstream, { method: 'POST', cache: 'no-store', headers: { 'Content-Type': 'application/json' }, body: '{}' });
  if (!response.ok) {
    const body = await response.text();
    return NextResponse.json({ error: body }, { status: response.status });
  }
  return new NextResponse(null, { status: 200 });
}
