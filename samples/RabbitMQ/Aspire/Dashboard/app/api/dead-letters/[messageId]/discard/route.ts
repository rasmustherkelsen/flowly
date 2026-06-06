import { NextResponse } from 'next/server';
import { getDashboardApiUrl } from '@/lib/dashboardApiUrl';

export async function DELETE(
  _request: Request,
  { params }: { params: Promise<{ messageId: string }> }
) {
  const { messageId } = await params;
  const upstream = `${getDashboardApiUrl()}/api/dead-letters/${encodeURIComponent(messageId)}/discard`;
  const response = await fetch(upstream, { method: 'DELETE', cache: 'no-store' });
  if (!response.ok) {
    const body = await response.text();
    return NextResponse.json({ error: body }, { status: response.status });
  }
  return new NextResponse(null, { status: 200 });
}
