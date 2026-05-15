import { cn } from "@/lib/utils";

export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-label="Carregando"
      className={cn("animate-pulse rounded-md bg-muted", className)}
      {...props}
    />
  );
}
