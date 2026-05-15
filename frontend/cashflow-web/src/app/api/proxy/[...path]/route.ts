import { NextResponse, type NextRequest } from "next/server";
import { getServerSession } from "next-auth";

import { authOptions } from "@/lib/auth";
import { serverEnv } from "@/lib/env";

export const dynamic = "force-dynamic";

const HOP_BY_HOP = new Set([
  "connection",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailer",
  "transfer-encoding",
  "upgrade",
  "host",
  "content-length",
]);

async function forward(request: NextRequest, params: { path: string[] }) {
  const session = await getServerSession(authOptions);
  if (!session?.accessToken) {
    return NextResponse.json(
      { error: "Sessão expirada ou não autenticada" },
      { status: 401 },
    );
  }

  const url = new URL(`${serverEnv.apiBaseUrl}/${params.path.join("/")}`);
  request.nextUrl.searchParams.forEach((value, key) => {
    url.searchParams.set(key, value);
  });

  const headers = new Headers();
  request.headers.forEach((value, key) => {
    if (!HOP_BY_HOP.has(key.toLowerCase())) headers.set(key, value);
  });
  headers.set("Authorization", `Bearer ${session.accessToken}`);
  headers.set("Accept", headers.get("Accept") ?? "application/json");

  const init: RequestInit = {
    method: request.method,
    headers,
    cache: "no-store",
    redirect: "manual",
  };

  if (request.method !== "GET" && request.method !== "HEAD") {
    init.body = await request.arrayBuffer();
  }

  const upstream = await fetch(url, init);
  const responseHeaders = new Headers();
  upstream.headers.forEach((value, key) => {
    if (!HOP_BY_HOP.has(key.toLowerCase())) responseHeaders.set(key, value);
  });

  return new NextResponse(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: responseHeaders,
  });
}

export async function GET(request: NextRequest, ctx: { params: { path: string[] } }) {
  return forward(request, ctx.params);
}
export async function POST(request: NextRequest, ctx: { params: { path: string[] } }) {
  return forward(request, ctx.params);
}
export async function PUT(request: NextRequest, ctx: { params: { path: string[] } }) {
  return forward(request, ctx.params);
}
export async function PATCH(request: NextRequest, ctx: { params: { path: string[] } }) {
  return forward(request, ctx.params);
}
export async function DELETE(request: NextRequest, ctx: { params: { path: string[] } }) {
  return forward(request, ctx.params);
}
