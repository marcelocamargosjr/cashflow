import "next-auth";
import "next-auth/jwt";

declare module "next-auth" {
  interface Session {
    accessToken?: string;
    merchantId: string | null;
    roles: string[];
    username: string | null;
    error?: string;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string;
    refreshToken?: string;
    expiresAt?: number;
    merchantId?: string | null;
    roles?: string[];
    username?: string | null;
    error?: string;
  }
}
