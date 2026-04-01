export type ExpenseCategory = "Ghost" | "Regular" | "Noise";

export interface Transaction {
  date: string;
  description: string;
  amount: number;
}

export interface RecurringGroup {
  merchantName: string;
  transactions: Transaction[];
  averageAmount: number;
  monthlyAmount: number;
  yearlyCost: number;
  occurrenceCount: number;
  category: ExpenseCategory;
}

export interface AnalysisResult {
  ghosts: RecurringGroup[];
  regulars: RecurringGroup[];
  totalTransactionsAnalyzed: number;
  totalMonthlyGhostCost: number;
   transactions: Transaction[]; 
}
