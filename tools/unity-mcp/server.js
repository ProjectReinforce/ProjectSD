"use strict";

const http = require("node:http");
const https = require("node:https");
const { URL } = require("node:url");

const SERVER_INFO = {
  name: "unity-mcp-server",
  version: "0.1.0"
};

const DEFAULT_PROTOCOL_VERSION = "2024-11-05";
const DEFAULT_BASE_URL = "http://127.0.0.1:51234/";
const DEFAULT_TIMEOUT_MS = 5000;

const baseUrl = normalizeBaseUrl(process.env.UNITY_MCP_BASE_URL || DEFAULT_BASE_URL);
const requestTimeoutMs = parsePositiveInt(process.env.UNITY_MCP_HTTP_TIMEOUT_MS, DEFAULT_TIMEOUT_MS);

const tools = [
  {
    name: "unity_health",
    description: "Check whether Unity editor bridge is alive and return editor state.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "unity_scene_current",
    description: "Get the currently active Unity scene information.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "unity_play_start",
    description: "Request Unity editor to enter Play Mode.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "unity_play_stop",
    description: "Request Unity editor to exit Play Mode.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "unity_console_errors",
    description: "Read recent Unity console errors, exceptions, and asserts.",
    inputSchema: {
      type: "object",
      properties: {
        limit: {
          type: "integer",
          minimum: 1,
          maximum: 100,
          description: "Maximum number of entries to return. Default is 20."
        }
      },
      additionalProperties: false
    }
  }
];

let stdinBuffer = Buffer.alloc(0);

process.stdin.on("data", (chunk) => {
  stdinBuffer = Buffer.concat([stdinBuffer, chunk]);
  processIncomingBuffer();
});

process.stdin.on("error", (error) => {
  logError("stdin error", error);
  process.exit(1);
});

process.stdin.on("end", () => {
  process.exit(0);
});

process.stdin.resume();

logInfo(`started; bridge=${baseUrl} timeout=${requestTimeoutMs}ms`);

function processIncomingBuffer() {
  while (true) {
    const headerEnd = stdinBuffer.indexOf("\r\n\r\n");
    if (headerEnd === -1) {
      return;
    }

    const headerText = stdinBuffer.slice(0, headerEnd).toString("utf8");
    const contentLength = extractContentLength(headerText);
    if (contentLength === null) {
      logError("missing or invalid Content-Length header");
      stdinBuffer = Buffer.alloc(0);
      return;
    }

    const bodyStart = headerEnd + 4;
    const messageEnd = bodyStart + contentLength;
    if (stdinBuffer.length < messageEnd) {
      return;
    }

    const body = stdinBuffer.slice(bodyStart, messageEnd);
    stdinBuffer = stdinBuffer.slice(messageEnd);

    let message;
    try {
      message = JSON.parse(body.toString("utf8"));
    } catch (error) {
      logError("invalid JSON payload", error);
      continue;
    }

    handleMessage(message).catch((error) => {
      logError("unhandled request error", error);
    });
  }
}

async function handleMessage(message) {
  if (!message || typeof message !== "object") {
    return;
  }

  if (typeof message.method !== "string") {
    return;
  }

  const hasId = Object.prototype.hasOwnProperty.call(message, "id");
  const id = hasId ? message.id : undefined;

  switch (message.method) {
    case "initialize":
      if (hasId) {
        sendResult(id, {
          protocolVersion: selectProtocolVersion(message.params),
          capabilities: {
            tools: {}
          },
          serverInfo: SERVER_INFO
        });
      }
      return;

    case "notifications/initialized":
      return;

    case "tools/list":
      if (hasId) {
        sendResult(id, { tools });
      }
      return;

    case "tools/call":
      if (!hasId) {
        return;
      }
      await handleToolsCall(id, message.params);
      return;

    default:
      if (hasId) {
        sendError(id, -32601, `Method not found: ${message.method}`);
      }
  }
}

async function handleToolsCall(id, params) {
  if (!params || typeof params !== "object") {
    sendError(id, -32602, "Invalid params");
    return;
  }

  const toolName = params.name;
  const args = params.arguments && typeof params.arguments === "object" ? params.arguments : {};

  if (typeof toolName !== "string" || toolName.length === 0) {
    sendError(id, -32602, "Missing tool name");
    return;
  }

  try {
    const payload = await callTool(toolName, args);
    sendResult(id, {
      content: [
        {
          type: "text",
          text: JSON.stringify(payload, null, 2)
        }
      ]
    });
  } catch (error) {
    sendResult(id, {
      isError: true,
      content: [
        {
          type: "text",
          text: formatErrorMessage(error)
        }
      ]
    });
  }
}

async function callTool(name, args) {
  switch (name) {
    case "unity_health":
      return requestUnityJson("GET", "/health");
    case "unity_scene_current":
      return requestUnityJson("GET", "/scene/current");
    case "unity_play_start":
      return requestUnityJson("POST", "/play/start");
    case "unity_play_stop":
      return requestUnityJson("POST", "/play/stop");
    case "unity_console_errors": {
      const limit = parsePositiveInt(args.limit, 20);
      const clamped = Math.max(1, Math.min(100, limit));
      return requestUnityJson("GET", `/console/errors?limit=${clamped}`);
    }
    default:
      throw new Error(`Unknown tool: ${name}`);
  }
}

function requestUnityJson(method, path) {
  return new Promise((resolve, reject) => {
    const url = new URL(path, baseUrl);
    const transport = url.protocol === "https:" ? https : http;
    const options = {
      method,
      hostname: url.hostname,
      port: url.port || (url.protocol === "https:" ? 443 : 80),
      path: `${url.pathname}${url.search}`,
      headers: {
        Accept: "application/json"
      }
    };

    const request = transport.request(options, (response) => {
      let body = "";
      response.setEncoding("utf8");
      response.on("data", (chunk) => {
        body += chunk;
      });
      response.on("end", () => {
        const statusCode = response.statusCode || 0;
        if (statusCode < 200 || statusCode >= 300) {
          reject(
            new Error(
              `Unity bridge request failed (${statusCode}) ${method} ${path}: ${truncate(body, 400)}`
            )
          );
          return;
        }

        if (body.trim().length === 0) {
          resolve({});
          return;
        }

        try {
          resolve(JSON.parse(body));
        } catch (error) {
          reject(new Error(`Unity bridge returned non-JSON response: ${truncate(body, 400)}`));
        }
      });
    });

    request.setTimeout(requestTimeoutMs, () => {
      request.destroy(new Error(`Unity bridge timeout after ${requestTimeoutMs}ms`));
    });

    request.on("error", (error) => {
      reject(error);
    });

    request.end();
  });
}

function sendResult(id, result) {
  sendMessage({
    jsonrpc: "2.0",
    id,
    result
  });
}

function sendError(id, code, message, data) {
  const error = {
    code,
    message
  };
  if (data !== undefined) {
    error.data = data;
  }

  sendMessage({
    jsonrpc: "2.0",
    id,
    error
  });
}

function sendMessage(message) {
  const body = Buffer.from(JSON.stringify(message), "utf8");
  const header = `Content-Length: ${body.length}\r\n\r\n`;
  process.stdout.write(header);
  process.stdout.write(body);
}

function extractContentLength(headerText) {
  const lines = headerText.split("\r\n");
  for (const line of lines) {
    const separator = line.indexOf(":");
    if (separator <= 0) {
      continue;
    }

    const key = line.slice(0, separator).trim().toLowerCase();
    if (key !== "content-length") {
      continue;
    }

    const value = Number.parseInt(line.slice(separator + 1).trim(), 10);
    if (Number.isFinite(value) && value >= 0) {
      return value;
    }
    return null;
  }

  return null;
}

function selectProtocolVersion(params) {
  if (params && typeof params === "object" && typeof params.protocolVersion === "string") {
    return params.protocolVersion;
  }
  return DEFAULT_PROTOCOL_VERSION;
}

function normalizeBaseUrl(value) {
  const trimmed = (value || "").trim();
  if (trimmed.length === 0) {
    return DEFAULT_BASE_URL;
  }
  return trimmed.endsWith("/") ? trimmed : `${trimmed}/`;
}

function parsePositiveInt(value, fallbackValue) {
  const parsed = Number.parseInt(value, 10);
  if (Number.isFinite(parsed) && parsed > 0) {
    return parsed;
  }
  return fallbackValue;
}

function truncate(value, maxLength) {
  if (typeof value !== "string") {
    return "";
  }
  if (value.length <= maxLength) {
    return value;
  }
  return value.slice(0, maxLength);
}

function formatErrorMessage(error) {
  if (!error) {
    return "Unknown error";
  }
  if (typeof error.message === "string" && error.message.length > 0) {
    return error.message;
  }
  return String(error);
}

function logInfo(message) {
  process.stderr.write(`[unity-mcp] ${message}\n`);
}

function logError(message, error) {
  if (error && error.message) {
    process.stderr.write(`[unity-mcp] ${message}: ${error.message}\n`);
    return;
  }
  process.stderr.write(`[unity-mcp] ${message}\n`);
}
