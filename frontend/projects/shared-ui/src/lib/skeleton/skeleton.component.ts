import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'yurt-skeleton',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="animate-pulse space-y-3">
      <div class="h-4 bg-stone-200 rounded-full w-3/4"></div>
      <div class="h-4 bg-stone-200 rounded-full"></div>
      <div class="h-4 bg-stone-200 rounded-full w-5/6"></div>
    </div>
  `,
})
export class SkeletonComponent {}

@Component({
  selector: 'yurt-skeleton-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bg-white rounded-2xl border border-stone-100 p-4 animate-pulse">
      <div class="h-32 bg-stone-200 rounded-xl mb-3"></div>
      <div class="h-4 bg-stone-200 rounded w-2/3 mb-2"></div>
      <div class="h-3 bg-stone-200 rounded w-full mb-2"></div>
      <div class="h-5 bg-amber-100 rounded w-1/4"></div>
    </div>
  `,
})
export class SkeletonCardComponent {}
