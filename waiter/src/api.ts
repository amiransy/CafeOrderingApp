import AsyncStorage from "@react-native-async-storage/async-storage";

const KEY = "apiUrl";
export const DEFAULT_API_URL = "https://henchman-ninth-shanty.ngrok-free.dev";

export async function getApiUrl(): Promise<string> {
  const saved = await AsyncStorage.getItem(KEY);
  return saved || DEFAULT_API_URL;
}

export async function setApiUrl(url: string): Promise<void> {
  await AsyncStorage.setItem(KEY, url.trim().replace(/\/$/, ""));
}

export async function resetApiUrl(): Promise<string> {
  await AsyncStorage.removeItem(KEY);
  return DEFAULT_API_URL;
}

export async function testConnection(url: string): Promise<{ ok: boolean; message: string }> {
  try {
    const clean = url.trim().replace(/\/$/, "");
    const res = await fetch(`${clean}/api/health`, {
      headers: { "ngrok-skip-browser-warning": "1" },
      signal: AbortSignal.timeout(3500),
    });
    if (res.ok) {
      return { ok: true, message: "Bağlantı başarılı (Kasa aktif)" };
    }
    return { ok: false, message: `Sunucu yanıt verdi ancak durum kodu: ${res.status}` };
  } catch (e) {
    return { ok: false, message: e instanceof Error ? e.message : "Sunucuya ulaşılamadı" };
  }
}

export async function resolveWorkingApiUrl(): Promise<string> {
  const saved = await AsyncStorage.getItem(KEY);
  if (saved) {
    try {
      const res = await fetch(`${saved.replace(/\/$/, "")}/api/health`, {
        headers: { "ngrok-skip-browser-warning": "1" },
        signal: AbortSignal.timeout(2500),
      });
      if (res.ok) return saved;
    } catch {
      // Saved url unreachable
    }
  }

  // Fallback to DEFAULT_API_URL if saved is unreachable or empty
  try {
    const res = await fetch(`${DEFAULT_API_URL.replace(/\/$/, "")}/api/health`, {
      headers: { "ngrok-skip-browser-warning": "1" },
      signal: AbortSignal.timeout(2500),
    });
    if (res.ok) {
      await AsyncStorage.setItem(KEY, DEFAULT_API_URL);
      return DEFAULT_API_URL;
    }
  } catch {
    // Default url also unreachable
  }

  return saved || DEFAULT_API_URL;
}

async function doFetch<T>(base: string, path: string, init?: RequestInit): Promise<T> {
  const cleanBase = base.trim().replace(/\/$/, "");
  const response = await fetch(`${cleanBase}${path}`, {
    ...init,
    signal: AbortSignal.timeout(6000),
    headers: {
      "Content-Type": "application/json",
      "ngrok-skip-browser-warning": "1",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `İstek başarısız (${response.status})`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const base = await getApiUrl();
  try {
    return await doFetch<T>(base, path, init);
  } catch (err) {
    if (base !== DEFAULT_API_URL) {
      try {
        const result = await doFetch<T>(DEFAULT_API_URL, path, init);
        await setApiUrl(DEFAULT_API_URL);
        return result;
      } catch {
        // Fallback failed
      }
    }
    throw err;
  }
}

export type TableDto = {
  id: number;
  number: number;
  name: string;
  section?: string;
  isOccupied?: boolean;
  totalAmount?: number;
  paidAmount?: number;
  remainingAmount?: number;
  itemCount?: number;
};
export type ProductDto = { id: number; name: string; price: number };
export type CategoryDto = { id: number; name: string; products: ProductDto[] };
export type TicketDto = {
  orderId: number;
  tableNumber: number;
  tableName: string;
  total: number;
};
export type TableAccountDetailDto = {
  accountId: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  items: {
    id: number;
    productName: string;
    quantity: number;
    unitPrice: number;
    lineTotal: number;
  }[];
};

export const api = {
  tables: () => request<TableDto[]>("/api/tables"),
  menu: () => request<CategoryDto[]>("/api/menu"),
  tableAccount: (tableId: number) => request<TableAccountDetailDto>(`/api/tables/${tableId}/account`),
  createOrder: (body: {
    tableId: number;
    note?: string;
    items: { productId: number; quantity: number }[];
    createdBy?: string;
  }) =>
    request<TicketDto>("/api/orders", {
      method: "POST",
      body: JSON.stringify({ ...body, createdBy: body.createdBy ?? "Garson" }),
    }),
};
