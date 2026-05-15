import { redirect } from "next/navigation";
import { getServerSession, type Session } from "next-auth";

import { authOptions } from "@/lib/auth";

export async function requireSession(callbackUrl: string): Promise<Session> {
  const session = await getServerSession(authOptions);
  if (!session?.accessToken || session.error === "RefreshAccessTokenError") {
    redirect(`/login?callbackUrl=${encodeURIComponent(callbackUrl)}`);
  }
  return session;
}
