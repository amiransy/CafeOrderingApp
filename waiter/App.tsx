import { StatusBar } from "expo-status-bar";
import { useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import {
  api,
  CategoryDto,
  DEFAULT_API_URL,
  getApiUrl,
  ProductDto,
  resetApiUrl,
  resolveWorkingApiUrl,
  setApiUrl,
  TableDto,
  testConnection,
} from "./src/api";

type Cart = Record<number, { product: ProductDto; quantity: number }>;

export default function App() {
  const [apiUrl, setApiUrlState] = useState(DEFAULT_API_URL);
  const [showSettings, setShowSettings] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tables, setTables] = useState<TableDto[]>([]);
  const [section, setSection] = useState("Sol");
  const [menu, setMenu] = useState<CategoryDto[]>([]);
  const [table, setTable] = useState<TableDto | null>(null);
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [cart, setCart] = useState<Cart>({});
  const [note, setNote] = useState("");
  const [sending, setSending] = useState(false);
  const [testing, setTesting] = useState(false);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const activeUrl = await resolveWorkingApiUrl();
      setApiUrlState(activeUrl);
      const [nextTables, nextMenu] = await Promise.all([api.tables(), api.menu()]);
      setTables(nextTables);
      setMenu(nextMenu);
      setCategoryId(nextMenu[0]?.id ?? null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sunucuya bağlanılamadı.");
    } finally {
      setLoading(false);
    }
  }

  async function handleTest(targetUrl?: string) {
    const toTest = targetUrl || apiUrl;
    setTesting(true);
    const result = await testConnection(toTest);
    setTesting(false);
    if (result.ok) {
      Alert.alert("Başarılı ✅", result.message);
    } else {
      Alert.alert("Bağlantı Başarısız ❌", result.message);
    }
  }

  async function handleResetDefault() {
    const def = await resetApiUrl();
    setApiUrlState(def);
    await load();
  }

  useEffect(() => {
    load();
  }, []);

  const items = useMemo(() => Object.values(cart), [cart]);
  const total = items.reduce((sum, i) => sum + i.product.price * i.quantity, 0);
  const products = menu.find((c) => c.id === categoryId)?.products ?? [];

  function add(product: ProductDto) {
    setCart((prev) => {
      const current = prev[product.id];
      return {
        ...prev,
        [product.id]: { product, quantity: (current?.quantity ?? 0) + 1 },
      };
    });
  }

  function remove(product: ProductDto) {
    setCart((prev) => {
      const current = prev[product.id];
      if (!current || current.quantity <= 1) {
        const next = { ...prev };
        delete next[product.id];
        return next;
      }
      return { ...prev, [product.id]: { ...current, quantity: current.quantity - 1 } };
    });
  }

  async function submit() {
    if (!table || items.length === 0) {
      return;
    }
    setSending(true);
    try {
      const ticket = await api.createOrder({
        tableId: table.id,
        note: note.trim() || undefined,
        items: items.map((i) => ({ productId: i.product.id, quantity: i.quantity })),
      });
      Alert.alert("Sipariş gönderildi", `${ticket.tableName} · #${ticket.orderId}`);
      setCart({});
      setNote("");
      setTable(null);
      await load();
    } catch (e) {
      Alert.alert("Hata", e instanceof Error ? e.message : "Sipariş gönderilemedi.");
    } finally {
      setSending(false);
    }
  }

  async function saveSettings() {
    await setApiUrl(apiUrl);
    setShowSettings(false);
    await load();
  }

  return (
    <View style={styles.screen}>
      <StatusBar style="light" />
      <View style={styles.header}>
        <Text style={styles.title}>{table ? table.name : "Masalar"}</Text>
        <View style={styles.headerActions}>
          {table ? (
            <Pressable onPress={() => setTable(null)} style={styles.headerBtn}>
              <Text style={styles.headerBtnText}>Masalar</Text>
            </Pressable>
          ) : null}
          <Pressable onPress={load} style={styles.headerBtn}>
            <Text style={styles.headerBtnText}>Yenile ↻</Text>
          </Pressable>
          <Pressable onPress={() => setShowSettings((v) => !v)} style={styles.headerBtn}>
            <Text style={styles.headerBtnText}>Ayar</Text>
          </Pressable>
        </View>
      </View>

      {showSettings ? (
        <View style={styles.settings}>
          <Text style={styles.label}>API adresi (kasa bilgisayarı)</Text>
          <TextInput
            value={apiUrl}
            onChangeText={setApiUrlState}
            autoCapitalize="none"
            autoCorrect={false}
            style={styles.input}
            placeholder={DEFAULT_API_URL}
            placeholderTextColor="#b9a390"
          />
          <View style={{ flexDirection: "row", gap: 8, marginVertical: 6 }}>
            <Pressable
              onPress={() => handleTest()}
              disabled={testing}
              style={[styles.primary, { flex: 1, backgroundColor: "#3a2c27" }]}
            >
              <Text style={styles.primaryText}>{testing ? "Sınanıyor..." : "Bağlantıyı Sına"}</Text>
            </Pressable>
            <Pressable
              onPress={handleResetDefault}
              style={[styles.primary, { flex: 1, backgroundColor: "#2b2320" }]}
            >
              <Text style={styles.primaryText}>Varsayılana Dön</Text>
            </Pressable>
          </View>
          <Pressable onPress={saveSettings} style={styles.primary}>
            <Text style={styles.primaryText}>Kaydet ve yenile</Text>
          </Pressable>
        </View>
      ) : null}

      {loading ? (
        <ActivityIndicator color="#f3d2b5" style={{ marginTop: 40 }} />
      ) : error ? (
        <View style={styles.settings}>
          <Text style={styles.error}>{error}</Text>
          <Text style={[styles.label, { fontSize: 13, marginTop: 6 }]}>
            Kasa bilgisayarı (API) adresi:
          </Text>
          <TextInput
            value={apiUrl}
            onChangeText={setApiUrlState}
            autoCapitalize="none"
            autoCorrect={false}
            style={styles.input}
            placeholder={DEFAULT_API_URL}
            placeholderTextColor="#b9a390"
          />
          <View style={{ flexDirection: "row", gap: 8, marginVertical: 6 }}>
            <Pressable
              onPress={() => handleTest()}
              disabled={testing}
              style={[styles.primary, { flex: 1, backgroundColor: "#3a2c27" }]}
            >
              <Text style={styles.primaryText}>{testing ? "Sınanıyor..." : "Bağlantıyı Sına"}</Text>
            </Pressable>
            <Pressable
              onPress={handleResetDefault}
              style={[styles.primary, { flex: 1, backgroundColor: "#4a3328" }]}
            >
              <Text style={styles.primaryText}>Varsayılana Sıfırla</Text>
            </Pressable>
          </View>
          <Pressable onPress={saveSettings} style={styles.primary}>
            <Text style={styles.primaryText}>Kaydet ve Tekrar Dene</Text>
          </Pressable>
        </View>
      ) : !table ? (
        <View style={{ flex: 1 }}>
          <View style={{ maxHeight: 52 }}>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.cats}>
              {["Sol", "Sağ", "Dış", "İç", "Kapı"].map((sec) => {
                const count = tables.filter((t) => t.section === sec && t.isOccupied).length;
                const total = tables.filter((t) => t.section === sec).length;
                const isSel = section === sec;
                return (
                  <Pressable
                    key={sec}
                    onPress={() => setSection(sec)}
                    style={[styles.cat, isSel && styles.catOn]}
                  >
                    <Text style={[styles.catText, isSel && styles.catTextOn]}>
                      {sec} ({count}/{total})
                    </Text>
                  </Pressable>
                );
              })}
            </ScrollView>
          </View>
          <ScrollView contentContainerStyle={styles.grid}>
            {tables.filter((t) => t.section === section).map((t) => {
              const isOcc = !!t.isOccupied;
              return (
                <Pressable
                  key={t.id}
                  onPress={() => setTable(t)}
                  style={[styles.table, isOcc && styles.tableOccupied]}
                >
                  <View style={styles.tableTop}>
                    <Text style={styles.tableNo}>{t.name}</Text>
                    <View style={[styles.badge, isOcc ? styles.badgeOcc : styles.badgeEmpty]}>
                      <Text style={styles.badgeText}>{isOcc ? "Dolu" : "Boş"}</Text>
                    </View>
                  </View>
                  <Text style={styles.tableName}>{t.section} Bölümü</Text>
                  {isOcc ? (
                    <Text style={styles.tableBalance}>{(t.remainingAmount ?? 0).toFixed(2)} ₺</Text>
                  ) : null}
                </Pressable>
              );
            })}
          </ScrollView>
        </View>
      ) : (
        <View style={{ flex: 1 }}>
          {table.isOccupied ? (
            <View style={styles.existingAccountBar}>
              <Text style={styles.existingAccountText}>
                ⚠️ Bu masada açık hesap var: {(table.remainingAmount ?? 0).toFixed(2)} ₺
              </Text>
              <Text style={[styles.existingAccountText, { fontSize: 11, color: "#d2b79b" }]}>
                (Gireceğiniz ürünler masanın mevcut hesabına eklenecektir)
              </Text>
            </View>
          ) : null}
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.cats}>
            {menu.map((c) => (
              <Pressable
                key={c.id}
                onPress={() => setCategoryId(c.id)}
                style={[styles.cat, categoryId === c.id && styles.catOn]}
              >
                <Text style={[styles.catText, categoryId === c.id && styles.catTextOn]}>{c.name}</Text>
              </Pressable>
            ))}
          </ScrollView>
          <ScrollView contentContainerStyle={styles.products}>
            {products.map((p) => {
              const qty = cart[p.id]?.quantity ?? 0;
              return (
                <View key={p.id} style={styles.product}>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.productName}>{p.name}</Text>
                    <Text style={styles.price}>{p.price.toFixed(2)} ₺</Text>
                  </View>
                  <View style={styles.qtyRow}>
                    <Pressable onPress={() => remove(p)} style={styles.qtyBtn}>
                      <Text style={styles.qtyBtnText}>−</Text>
                    </Pressable>
                    <Text style={styles.qty}>{qty}</Text>
                    <Pressable onPress={() => add(p)} style={styles.qtyBtn}>
                      <Text style={styles.qtyBtnText}>+</Text>
                    </Pressable>
                  </View>
                </View>
              );
            })}
            <TextInput
              value={note}
              onChangeText={setNote}
              placeholder="Sipariş notu (isteğe bağlı)"
              placeholderTextColor="#b9a390"
              style={styles.input}
            />
          </ScrollView>
          <View style={styles.footer}>
            <Text style={styles.total}>
              {items.reduce((n, i) => n + i.quantity, 0)} ürün · {total.toFixed(2)} ₺
            </Text>
            <Pressable
              onPress={submit}
              disabled={sending || items.length === 0}
              style={[styles.primary, (sending || items.length === 0) && { opacity: 0.5 }]}
            >
              <Text style={styles.primaryText}>{sending ? "Gönderiliyor…" : "Siparişi onayla"}</Text>
            </Pressable>
          </View>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: "#1c140e", paddingTop: 52 },
  header: {
    paddingHorizontal: 20,
    paddingBottom: 12,
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  title: { color: "#f6efe4", fontSize: 28, fontWeight: "700" },
  headerActions: { flexDirection: "row", gap: 8 },
  headerBtn: { backgroundColor: "#3a2a20", paddingHorizontal: 12, paddingVertical: 8, borderRadius: 8 },
  headerBtnText: { color: "#f3d2b5", fontWeight: "600" },
  settings: { padding: 20, gap: 12 },
  label: { color: "#f3d2b5" },
  input: {
    backgroundColor: "#2b2118",
    color: "#f6efe4",
    borderRadius: 10,
    padding: 12,
    borderWidth: 1,
    borderColor: "#4a382c",
  },
  error: { color: "#ffb4a8" },
  grid: { padding: 16, flexDirection: "row", flexWrap: "wrap", gap: 12 },
  table: {
    width: "30%",
    minWidth: 96,
    flexGrow: 1,
    backgroundColor: "#1e532b",
    borderRadius: 14,
    paddingVertical: 14,
    paddingHorizontal: 8,
    alignItems: "center",
  },
  tableOccupied: {
    backgroundColor: "#932f1a",
  },
  tableTop: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    width: "100%",
    paddingHorizontal: 6,
  },
  badge: {
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 6,
  },
  badgeOcc: { backgroundColor: "#d94320" },
  badgeEmpty: { backgroundColor: "#2b773d" },
  badgeText: { color: "#fff", fontSize: 10, fontWeight: "700" },
  tableNo: { color: "#fff", fontSize: 24, fontWeight: "800" },
  tableName: { color: "#f6efe4", marginTop: 2, fontSize: 13 },
  tableBalance: { color: "#f8d39e", fontSize: 13, fontWeight: "700", marginTop: 4 },
  existingAccountBar: {
    backgroundColor: "#3a2214",
    borderBottomWidth: 1,
    borderBottomColor: "#5c3924",
    paddingHorizontal: 16,
    paddingVertical: 8,
    alignItems: "center",
    marginBottom: 8,
  },
  existingAccountText: {
    color: "#f8d39e",
    fontWeight: "700",
    fontSize: 13,
    textAlign: "center",
  },
  cats: { paddingHorizontal: 16, gap: 8, paddingBottom: 8 },
  cat: { backgroundColor: "#2b2118", paddingHorizontal: 14, paddingVertical: 10, borderRadius: 20 },
  catOn: { backgroundColor: "#c4a574" },
  catText: { color: "#f3d2b5", fontWeight: "600" },
  catTextOn: { color: "#1c140e" },
  products: { padding: 16, gap: 10, paddingBottom: 24 },
  product: {
    backgroundColor: "#2b2118",
    borderRadius: 12,
    padding: 14,
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
  },
  productName: { color: "#f6efe4", fontSize: 16, fontWeight: "600" },
  price: { color: "#c4a574", marginTop: 4 },
  qtyRow: { flexDirection: "row", alignItems: "center", gap: 10 },
  qtyBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: "#8b3a1d",
    alignItems: "center",
    justifyContent: "center",
  },
  qtyBtnText: { color: "#fff", fontSize: 20, fontWeight: "700" },
  qty: { color: "#f6efe4", width: 20, textAlign: "center", fontSize: 16 },
  footer: {
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: "#4a382c",
    gap: 10,
  },
  total: { color: "#f6efe4", fontSize: 16, fontWeight: "700" },
  primary: { backgroundColor: "#c4a574", borderRadius: 12, padding: 14, alignItems: "center" },
  primaryText: { color: "#1c140e", fontWeight: "800", fontSize: 16 },
});
