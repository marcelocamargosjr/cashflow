"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useQueryClient } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { useToast } from "@/components/ui/use-toast";
import { apiFetch, ApiError } from "@/lib/api-client";
import type { Entry } from "@/lib/types";
import { toIsoDate } from "@/lib/utils";
import { uuidv7 } from "@/lib/uuid";

function isoTomorrow(): string {
  const date = new Date();
  date.setDate(date.getDate() + 1);
  return toIsoDate(date);
}

const schema = z.object({
  type: z.enum(["Credit", "Debit"], {
    required_error: "Selecione o tipo do lançamento",
  }),
  amount: z
    .string()
    .min(1, "Informe o valor")
    .refine((value) => /^\d+([.,]\d{1,2})?$/.test(value.trim()), {
      message: "Use apenas números com até 2 casas decimais",
    })
    .transform((value) => Number(value.replace(",", ".")))
    .pipe(z.number().positive("O valor deve ser maior que zero")),
  description: z
    .string()
    .trim()
    .min(1, "A descrição é obrigatória")
    .max(200, "Máximo de 200 caracteres"),
  category: z
    .string()
    .trim()
    .max(50, "Máximo de 50 caracteres")
    .optional()
    .transform((value) => (value ? value : undefined)),
  entryDate: z
    .string()
    .min(1, "Informe a data")
    .refine((value) => value <= isoTomorrow(), {
      message: "Data não pode ser posterior a amanhã",
    }),
});

type FormInput = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

export function NewEntryForm() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: {
      type: "Credit",
      amount: "",
      description: "",
      category: "",
      entryDate: toIsoDate(new Date()),
    },
    mode: "onBlur",
  });
  const [submitting, setSubmitting] = React.useState(false);

  async function onSubmit(values: FormOutput) {
    setSubmitting(true);
    try {
      const created = await apiFetch<Entry>("/ledger/api/v1/entries", {
        method: "POST",
        headers: { "Idempotency-Key": uuidv7() },
        body: {
          type: values.type,
          amount: { value: values.amount, currency: "BRL" },
          description: values.description,
          category: values.category ?? null,
          entryDate: values.entryDate,
        },
      });
      toast({
        title: "Lançamento registrado",
        description: `ID ${created.id.slice(0, 8)}… criado com sucesso.`,
        variant: "success",
      });
      queryClient.invalidateQueries({ queryKey: ["entries"] });
      queryClient.invalidateQueries({ queryKey: ["daily-balance"] });
      router.push("/entries");
      router.refresh();
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.problem?.errors) {
          Object.entries(error.problem.errors).forEach(([field, messages]) => {
            const path = field
              .replace(/^amount\.value$/, "amount")
              .split(".")[0] as keyof FormInput;
            form.setError(path, { message: messages.join(", ") });
          });
        }
        toast({
          title: "Não foi possível registrar o lançamento",
          description: error.message,
          variant: "destructive",
        });
      } else {
        toast({
          title: "Falha inesperada",
          description: "Tente novamente em instantes.",
          variant: "destructive",
        });
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Form {...form}>
      <form
        className="space-y-6"
        onSubmit={form.handleSubmit(onSubmit)}
        noValidate
        aria-busy={submitting}
      >
        <FormField
          control={form.control}
          name="type"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Tipo *</FormLabel>
              <FormControl>
                <select
                  {...field}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                >
                  <option value="Credit">Crédito (entrada)</option>
                  <option value="Debit">Débito (saída)</option>
                </select>
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="amount"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Valor *</FormLabel>
              <FormControl>
                <Input
                  {...field}
                  inputMode="decimal"
                  placeholder="0,00"
                  autoComplete="off"
                />
              </FormControl>
              <FormDescription>Use ponto ou vírgula como separador decimal.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="description"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Descrição *</FormLabel>
              <FormControl>
                <Input {...field} maxLength={200} placeholder="Ex.: Venda balcão #123" />
              </FormControl>
              <FormDescription>Até 200 caracteres.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="category"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Categoria</FormLabel>
              <FormControl>
                <Input {...field} maxLength={50} placeholder="Ex.: Sales" />
              </FormControl>
              <FormDescription>Opcional. Usado no consolidado por categoria.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="entryDate"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Data do lançamento *</FormLabel>
              <FormControl>
                <Input type="date" {...field} max={isoTomorrow()} />
              </FormControl>
              <FormDescription>
                Pode ser de até um dia no futuro para suportar lançamentos próximos à virada do dia.
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <div className="flex items-center justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push("/entries")}
            disabled={submitting}
          >
            Cancelar
          </Button>
          <Button type="submit" disabled={submitting} aria-busy={submitting}>
            {submitting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
                Registrando…
              </>
            ) : (
              "Registrar lançamento"
            )}
          </Button>
        </div>
      </form>
    </Form>
  );
}
