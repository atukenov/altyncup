import { TestBed } from '@angular/core/testing';

import { OtpBoxesComponent } from './otp-boxes.component';

describe('OtpBoxesComponent', () => {
  const boxes = (el: HTMLElement) => el.querySelectorAll('input').length;

  it('shows 4 boxes by default and 6 for Firebase SMS codes', async () => {
    const fixture = TestBed.createComponent(OtpBoxesComponent);
    await fixture.whenStable();
    expect(boxes(fixture.nativeElement)).toBe(4);

    fixture.componentRef.setInput('length', 6);
    await fixture.whenStable();
    expect(boxes(fixture.nativeElement)).toBe(6);
    expect(fixture.componentInstance.compact).toBe(true);
  });

  it('clears what was typed when the length changes', async () => {
    const fixture = TestBed.createComponent(OtpBoxesComponent);
    const emitted: string[] = [];
    fixture.componentInstance.codeChange.subscribe((c) => emitted.push(c));
    await fixture.whenStable();

    const first = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    first.value = '7';
    first.dispatchEvent(new Event('input'));
    expect(emitted.at(-1)).toBe('7');

    fixture.componentRef.setInput('length', 6);
    expect(emitted.at(-1)).toBe('');
    expect(fixture.componentInstance.digits).toEqual(['', '', '', '', '', '']);
  });
});
