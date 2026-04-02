import { useMemo } from "react";
import type { AnalysisResult, RecurringGroup, Transaction } from "./types/api";

export type TopMerchant = {
  merchantName: string;
  totalSpent: number;
};

export type MerchantTrend = {
  key: string;
  points: number[];
  normalized: number[];
};

function groupKey(group: RecurringGroup): string {
  return `${group.category}:${group.merchantName}`;
}

function buildTrend(transactions: Transaction[]): MerchantTrend {
  const ordered = [...transactions].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());
  const points = ordered.map((tx) => Math.abs(tx.amount));

  if (points.length <= 1) {
    const single = points.length === 0 ? [0, 0] : [points[0], points[0]];
    return {
      key: "",
      points: single,
      normalized: [0.5, 0.5],
    };
  }

  const min = Math.min(...points);
  const max = Math.max(...points);
  const normalized = points.map((point) => {
    if (max === min) {
      return 0.5;
    }

    return (point - min) / (max - min);
  });

  return {
    key: "",
    points,
    normalized,
  };
}

export function useAnalysisMemo(result: AnalysisResult | null) {
  const hasRecurring = useMemo(() => {
    if (!result) {
      return false;
    }

    return result.ghosts.length > 0 || result.regulars.length > 0;
  }, [result]);

  const recurringGroups = useMemo(() => {
    if (!result) {
      return [] as RecurringGroup[];
    }

    return [...result.ghosts, ...result.regulars];
  }, [result]);

  const topSpendingMerchants = useMemo<TopMerchant[]>(() => {
    if (!result) {
      return [];
    }

    if (recurringGroups.length > 0) {
      const totals: Record<string, number> = recurringGroups.reduce((acc, group) => {
        const total = group.transactions.reduce((sum, tx) => sum + Math.abs(tx.amount), 0);
        acc[group.merchantName] = (acc[group.merchantName] ?? 0) + total;
        return acc;
      }, {} as Record<string, number>);

      return Object.entries(totals)
        .filter(([name]) => name.trim() !== "")
        .sort((a, b) => b[1] - a[1])
        .slice(0, 3)
        .map(([merchantName, totalSpent]) => ({ merchantName, totalSpent }));
    }

    const totals: Record<string, number> = (result.transactions ?? []).reduce((acc, tx) => {
      const name = tx.description.trim();
      if (!name) {
        return acc;
      }

      acc[name] = (acc[name] ?? 0) + Math.abs(tx.amount);
      return acc;
    }, {} as Record<string, number>);

    return Object.entries(totals)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 3)
      .map(([merchantName, totalSpent]) => ({ merchantName, totalSpent }));
  }, [result, recurringGroups]);

  const maxRecurringYearly = useMemo(() => {
    if (!result) {
      return 0;
    }

    const values = recurringGroups.map((group) => Math.abs(group.yearlyCost));
    return values.length > 0 ? Math.max(...values) : 0;
  }, [result, recurringGroups]);

  const trendMap = useMemo(() => {
    const map = new Map<string, MerchantTrend>();

    for (const group of recurringGroups) {
      const key = groupKey(group);
      const trend = buildTrend(group.transactions);
      map.set(key, { ...trend, key });
    }

    return map;
  }, [recurringGroups]);

  const revealDelayMap = useMemo(() => {
    const sorted = [...recurringGroups].sort((a, b) => Math.abs(b.yearlyCost) - Math.abs(a.yearlyCost));
    const map = new Map<string, number>();

    sorted.forEach((group, index) => {
      map.set(groupKey(group), index * 45);
    });

    return map;
  }, [recurringGroups]);

  return {
    hasRecurring,
    topSpendingMerchants,
    maxRecurringYearly,
    recurringGroups,
    trendMap,
    revealDelayMap,
  };
}
