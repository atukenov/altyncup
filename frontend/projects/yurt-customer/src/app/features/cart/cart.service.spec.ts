import { TestBed } from '@angular/core/testing';
import { Order } from 'shared-models';
import { StoreStatusService } from '../../core/store-status.service';
import { CartService } from './cart.service';

describe('CartService', () => {
  let open = true;
  let cart: CartService;

  beforeEach(() => {
    localStorage.clear();
    open = true;
    TestBed.configureTestingModule({
      providers: [{ provide: StoreStatusService, useValue: { guardOpen: () => open } }],
    });
    cart = TestBed.inject(CartService);
  });

  it('merges the same item, size and toppings into one line', () => {
    const latte = { menuItemId: 'latte', name: 'Latte', price: 1000, quantity: 1, variantId: 'L' };
    cart.addItem(latte);
    cart.addItem({ ...latte, quantity: 2 });
    cart.addItem({ ...latte, variantId: 'M' });

    expect(cart.items().length).toBe(2);
    expect(cart.items()[0].quantity).toBe(3);
  });

  it('includes toppings in the subtotal', () => {
    cart.addItem({
      menuItemId: 'latte', name: 'Latte', price: 1000, quantity: 2,
      selectedToppings: [{ toppingId: 'syrup', toppingName: 'Syrup', price: 200 }],
    });
    expect(cart.subtotal()).toBe(2400);
  });

  it('does not add anything while the cafe is closed', () => {
    open = false;
    expect(cart.addItem({ menuItemId: 'x', name: 'X', price: 1, quantity: 1 })).toBe(false);
    expect(cart.items().length).toBe(0);
  });

  it('re-orders a past order with its size, and without counting toppings twice', () => {
    const order = {
      items: [
        {
          id: '1', menuItemId: 'latte', menuItemName: 'Latte', quantity: 2,
          unitPrice: 1200, lineTotal: 2400, variantId: 'L', variantLabel: 'Large',
          toppings: [{ toppingId: 'syrup', toppingName: 'Syrup', price: 200 }],
        },
      ],
    } as unknown as Order;

    expect(cart.addFromOrder(order)).toBe(true);
    const line = cart.items()[0];
    expect(line.variantId).toBe('L');
    expect(line.price).toBe(1000);
    // Same total the customer paid the first time.
    expect(cart.subtotal()).toBe(2400);
  });
});
