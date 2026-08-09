export interface ServerInfo {
  serverName: string;
  mode: "lan" | "wan";
  httpPort: number;
  discoveryPort: number;
  version: string;
}

export async function fetchServerInfo(): Promise<ServerInfo> {
  const res = await fetch("/api/server/info");
  if (!res.ok) throw new Error(`server/info failed: ${res.status}`);
  return res.json();
}
