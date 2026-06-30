import {
  Directive,
  ElementRef,
  EventEmitter,
  HostListener,
  OnDestroy,
  OnInit,
  Output,
  Renderer2,
} from '@angular/core';

const THRESHOLD = 72;

@Directive({
  selector: '[pullToRefresh]',
  standalone: true,
})
export class PullToRefreshDirective implements OnInit, OnDestroy {
  @Output() refreshed = new EventEmitter<void>();

  private startY = 0;
  private pulling = false;
  private indicator!: HTMLElement;

  constructor(private el: ElementRef<HTMLElement>, private renderer: Renderer2) {}

  ngOnInit(): void {
    this.indicator = this.renderer.createElement('div') as HTMLElement;
    this.renderer.setStyle(this.indicator, 'position', 'fixed');
    this.renderer.setStyle(this.indicator, 'top', 'calc(env(safe-area-inset-top) + 8px)');
    this.renderer.setStyle(this.indicator, 'left', '0');
    this.renderer.setStyle(this.indicator, 'right', '0');
    this.renderer.setStyle(this.indicator, 'display', 'flex');
    this.renderer.setStyle(this.indicator, 'justify-content', 'center');
    this.renderer.setStyle(this.indicator, 'pointer-events', 'none');
    this.renderer.setStyle(this.indicator, 'opacity', '0');
    this.renderer.setStyle(this.indicator, 'transition', 'opacity 0.2s');
    this.renderer.setStyle(this.indicator, 'z-index', '9999');
    this.indicator.innerHTML = `
      <div style="width:32px;height:32px;border-radius:50%;border:3px solid #f59e0b;border-top-color:transparent;animation:ptr-spin 0.7s linear infinite;background:white;box-shadow:0 2px 8px rgba(0,0,0,0.15)"></div>
      <style>@keyframes ptr-spin{to{transform:rotate(360deg)}}</style>
    `;
    document.body.appendChild(this.indicator);
  }

  ngOnDestroy(): void {
    this.indicator?.remove();
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(e: TouchEvent): void {
    if (window.scrollY === 0) {
      this.startY = e.touches[0].clientY;
      this.pulling = true;
    }
  }

  @HostListener('touchmove', ['$event'])
  onTouchMove(e: TouchEvent): void {
    if (!this.pulling) return;
    const dy = e.touches[0].clientY - this.startY;
    if (dy > 10 && window.scrollY === 0) {
      const ratio = Math.min(dy / THRESHOLD, 1);
      this.renderer.setStyle(this.indicator, 'opacity', String(ratio));
    } else if (dy <= 0) {
      this.pulling = false;
      this.renderer.setStyle(this.indicator, 'opacity', '0');
    }
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(e: TouchEvent): void {
    if (!this.pulling) return;
    const dy = e.changedTouches[0].clientY - this.startY;
    this.renderer.setStyle(this.indicator, 'opacity', '0');
    this.pulling = false;
    if (dy >= THRESHOLD) {
      this.refreshed.emit();
    }
  }
}
