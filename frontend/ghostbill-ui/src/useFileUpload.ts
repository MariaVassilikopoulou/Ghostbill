import { useCallback, useEffect, useRef, useState } from "react";
import type { AnalysisResult } from "./types/api";

type UploadErrorCode =
  | "INVALID_FILE"
  | "UNSUPPORTED_FORMAT"
  | "PARSE_ERROR"
  | "NO_DATA_FOUND"
  | "UNKNOWN";

type UploadError = {
  code: UploadErrorCode;
  message: string;
};

const ACCEPTED_EXTENSIONS = [".csv", ".xlsx"];
const ACCEPTED_MIME = [
  "text/csv",
  "application/csv",
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
];

function isAcceptedFile(file: File): boolean {
  const lowerName = file.name.toLowerCase();
  const hasValidExtension = ACCEPTED_EXTENSIONS.some((ext) => lowerName.endsWith(ext));
  const hasValidMime = !file.type || ACCEPTED_MIME.includes(file.type);
  return hasValidExtension && hasValidMime;
}

function normalizeError(payload: unknown): UploadError {
  if (payload && typeof payload === "object") {
    const typed = payload as { code?: string; message?: string };
    if (typed.code && typed.message) {
      if (
        typed.code === "INVALID_FILE" ||
        typed.code === "UNSUPPORTED_FORMAT" ||
        typed.code === "PARSE_ERROR" ||
        typed.code === "NO_DATA_FOUND"
      ) {
        return { code: typed.code, message: typed.message };
      }

      return { code: "UNKNOWN", message: typed.message };
    }
  }

  return { code: "UNKNOWN", message: "Analysis failed" };
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

async function estimateCsvRows(file: File): Promise<number | null> {
  if (!file.name.toLowerCase().endsWith(".csv")) {
    return null;
  }

  try {
    // Sample first 256KB for a fast estimate on large files.
    const chunk = await file.slice(0, 256 * 1024).text();
    if (!chunk) {
      return 0;
    }

    const lineBreaks = chunk.match(/\r\n|\n|\r/g)?.length ?? 0;
    const estimate = lineBreaks + 1;
    return Math.max(estimate, 0);
  } catch {
    return null;
  }
}

export function useFileUpload() {
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const [progress, setProgress] = useState(0);
  const [toast, setToast] = useState<string | null>(null);
  const [fileSizeLabel, setFileSizeLabel] = useState<string | null>(null);
  const [csvEstimatedRows, setCsvEstimatedRows] = useState<number | null>(null);

  const abortRef = useRef<AbortController | null>(null);
  const latestRequestIdRef = useRef(0);
  const progressTimerRef = useRef<number | null>(null);

  const clearProgressTimer = useCallback(() => {
    if (progressTimerRef.current !== null) {
      window.clearInterval(progressTimerRef.current);
      progressTimerRef.current = null;
    }
  }, []);

  useEffect(
    () => () => {
      abortRef.current?.abort();
      clearProgressTimer();
    },
    [clearProgressTimer],
  );

  const showToast = useCallback((message: string) => {
    setToast(message);
  }, []);

  const dismissToast = useCallback(() => {
    setToast(null);
  }, []);

  const resetUpload = useCallback(() => {
    abortRef.current?.abort();
    clearProgressTimer();
    setFile(null);
    setFileSizeLabel(null);
    setCsvEstimatedRows(null);
    setProgress(0);
    setDragActive(false);
    setLoading(false);
    setToast(null);
  }, [clearProgressTimer]);

  const onFileSelected = useCallback(
    async (nextFile: File | null) => {
      if (!nextFile) {
        setFile(null);
        setFileSizeLabel(null);
        setCsvEstimatedRows(null);
        return;
      }

      if (!isAcceptedFile(nextFile)) {
        showToast("Only CSV/XLSX allowed");
        return;
      }

      setFile(nextFile);
      setFileSizeLabel(formatFileSize(nextFile.size));
      setCsvEstimatedRows(await estimateCsvRows(nextFile));
    },
    [showToast],
  );

  const setDragState = useCallback((active: boolean) => {
    setDragActive(active);
  }, []);

  const analyze = useCallback(async () => {
    if (!file) {
      showToast("Please select a CSV/XLSX file");
      return;
    }

    abortRef.current?.abort();

    const controller = new AbortController();
    abortRef.current = controller;
    const requestId = ++latestRequestIdRef.current;
    const previous = result;

    setLoading(true);
    setProgress(12);
    clearProgressTimer();
    progressTimerRef.current = window.setInterval(() => {
      setProgress((current) => (current >= 85 ? current : current + 7));
    }, 180);

    try {
      const formData = new FormData();
      formData.append("csvFile", file);

      const response = await fetch("http://localhost:5094/api/transactions/analyze", {
        method: "POST",
        body: formData,
        signal: controller.signal,
      });

      if (!response.ok) {
        let errorPayload: unknown = null;
        try {
          errorPayload = await response.json();
        } catch {
          errorPayload = null;
        }

        const normalized = normalizeError(errorPayload);
        throw normalized;
      }

      const analysis = (await response.json()) as AnalysisResult;

      if (requestId !== latestRequestIdRef.current) {
        return;
      }

      setResult(analysis);
      setToast(null);
    } catch (err) {
      if (controller.signal.aborted || requestId !== latestRequestIdRef.current) {
        return;
      }

      const normalized: UploadError =
        err && typeof err === "object" && "message" in err
          ? normalizeError(err)
          : { code: "UNKNOWN", message: "Analysis failed" };

      setResult(previous);
      showToast(normalized.message);
    } finally {
      if (requestId === latestRequestIdRef.current) {
        clearProgressTimer();
        setProgress(100);
        window.setTimeout(() => {
          if (requestId === latestRequestIdRef.current) {
            setProgress(0);
            setLoading(false);
          }
        }, 200);
      }
    }
  }, [clearProgressTimer, file, result, showToast]);

  return {
    file,
    result,
    loading,
    dragActive,
    progress,
    toast,
    fileSizeLabel,
    csvEstimatedRows,
    setResult,
    dismissToast,
    resetUpload,
    onFileSelected,
    setDragState,
    analyze,
  };
}
