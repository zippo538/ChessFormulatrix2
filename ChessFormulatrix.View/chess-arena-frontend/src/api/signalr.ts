import * as signalR from '@microsoft/signalr';

function getHubUrl(hubPath: string): string {
  const base = import.meta.env.VITE_CHESS_API_URL 
    ? new URL(import.meta.env.VITE_CHESS_API_URL, window.location.origin).origin 
    : window.location.origin;
  return `${base}${hubPath}`;
}

export function createSignalRConnection(hubPath: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(getHubUrl(hubPath), {
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();
}
