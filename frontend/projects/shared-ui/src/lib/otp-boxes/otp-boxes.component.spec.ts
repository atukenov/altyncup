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

  it('spreads a whole code from keyboard autofill over all boxes', async () => {
    const fixture = TestBed.createComponent(OtpBoxesComponent);
    const emitted: string[] = [];
    fixture.componentInstance.codeChange.subscribe((c) => emitted.push(c));
    await fixture.whenStable();

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    inputs[0].value = '4821';
    inputs[0].dispatchEvent(new Event('input'));
    expect(emitted.at(-1)).toBe('4821');
    expect(Array.from(inputs).map((i) => i.value)).toEqual(['4', '8', '2', '1']);
  });

  it('fills from the first box when a full code is typed into a later one', async () => {
    const fixture = TestBed.createComponent(OtpBoxesComponent);
    fixture.componentRef.setInput('length', 6);
    await fixture.whenStable();

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    inputs[2].value = '123456';
    inputs[2].dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.digits.join('')).toBe('123456');
  });

  it('replaces the digit when typing over a filled box', async () => {
    const fixture = TestBed.createComponent(OtpBoxesComponent);
    await fixture.whenStable();

    const first = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    first.value = '3';
    first.dispatchEvent(new Event('input'));
    first.value = '35';
    first.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.digits).toEqual(['5', '', '', '']);
  });
});
