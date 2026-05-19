# TODO

- Login / Register phone input
  - Always display the phone code prefix `+7` in the mobile phone field.
  - User should enter only the 10-digit number after `+7`.
  - Validate the input format as `+7` followed by 10 digits.

- PIN login behavior
  - When the user types a 4-digit PIN, automatically attempt sign in.
  - If the PIN is correct, proceed to login without requiring the user to press the "Sign in" button.
  - If the PIN is incorrect, show an error message.

- Menu item toppings / add-ons feature
  - For menu categories like `Coffee` and `Cold Drinks`, show optional toppings/additional items when adding an item.
  - Examples: oat milk, almond milk, no sugar, extra hot, etc.
  - Store all toppings and additional items in the database.
  - Manage toppings/add-ons from the "Menu Management" screen.

- Persistent customer login
  - When a customer logs in, save the session so the customer remains logged in.
  - The login should expire only after one week of inactivity.
  - If the customer logs in again within a week, refresh the expiration for another week.

- Company Logo
  - Need udpate favicon of the application, and change logo.png as logo for the company.
