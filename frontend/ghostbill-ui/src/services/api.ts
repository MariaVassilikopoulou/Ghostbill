import type { AnalysisResult } from "../types/api";

export async function analyzeTransactions(file: File): Promise<AnalysisResult> {
  const formData = new FormData();
  formData.append("csvFile", file);

  const response = await fetch("http://localhost:5094/api/transactions/analyze", {
    method: "POST",
    body: formData,
  });

  if (!response.ok) {
    throw new Error("Analysis failed");
  }

  return (await response.json()) as AnalysisResult;
}