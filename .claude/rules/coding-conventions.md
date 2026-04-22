# General Coding Conventions

- Make the code testable
- Write Unit Test
- Adhere to the SOLID principles

## Readability

- It is crucial that the code is easily readable.
- Prefer good structure and naming over code comments.

Example:

```C#
  
  // Customer is gold member
  if(customerPoint > 100)
  {
    ...
  }
```

This should be:

```C#
  
  if(IsGoldMember(customerPoint))
  {
    ...
  }

  private bool IsGoldMember(int points) => points > 100;
```

This clearly communicates the business logic without writing comments

## SOLID Principles

- It is crucial that classes are implemented in a way that makes them testable for example by ensuring that injected dependencies are interfaces or easy-to-replace implementations during test.

## Private methods

- When writing a private method strongly consider if the method should actually be placed on a different class as a public method instead. Sometimes a private method means the code is not correctly structured or there is some undiscovered concept. This of course does not go for small helper methods that makes the code clean and easy to read.

## Primary Constructors

- If parameters are just passed in through the constructor and used without modification use primary constructor but **only** if the class is internal.

