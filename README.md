# Virto Commerce X-Pickup Module

The X-Pickup module enables customers to select pickup points for their orders within the Virto Commerce platform. This module provides a seamless solution for integrating pickup options into the checkout experience, allowing customers to choose from a list of available pickup locations.

<img width="1415" height="957" alt="image" src="https://github.com/user-attachments/assets/577745c3-941e-42c1-9366-3a39103af80a" />
<img width="1423" height="667" alt="image" src="https://github.com/user-attachments/assets/577d0f6f-d4e0-479c-b238-32058ef6c4f6" />
<img width="329" height="642" alt="image" src="https://github.com/user-attachments/assets/560a5dca-809a-4393-b6b1-00587e592798" />

## Key features

* Configurable product pickup location details (fulfillment centers, address, geolocation)
* Integration with inventory data (inventory tracking, available in stock quantity)
* Available pickup points in product details page
* Pickup point selection and confirmation in customer cart

## Screenshots
<img width="1372" height="881" alt="image" src="https://github.com/user-attachments/assets/29c6138a-3a4d-4e36-a490-5756dbd00aeb" />
<img width="980" height="460" alt="image" src="https://github.com/user-attachments/assets/0dc659fb-4bf0-465e-9314-240dba1c9a4d" />


## XAPI Specification

---
### Queries
```js
{
  productPickupLocations(
    storeId: $storeId
    cultureName: $cultureName
    productId: $productId
    keyword: $keyword
    first: $first
    after: $after
    sort: $sort
  ) {
    totalCount
    items {
      id
      name
      description
      contactEmail
      contactPhone
      workingHours
      geoLocation
      availabilityType
      availableQuantity
      availabilityNote
      address {
        id
        line1
        line2
        city
        countryName
        countryCode
        regionId
        postalCode
        phone
      }
    }
  }
}
```

```js
{
  cartPickupLocations(
    storeId: $storeId
    cultureName: $cultureName
    cartId: $cartId
    keyword: $keyword
    first: $first
    after: $after
    sort: $sort
  ) {
    totalCount
    items {
      id
      name
      description
      contactEmail
      contactPhone
      workingHours
      geoLocation
      availabilityType
      availableQuantity
      availabilityNote
      address {
        id
        line1
        line2
        city
        countryName
        countryCode
        regionId
        postalCode
        phone
      }
    }
  }
}
```

## References
* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-push-messages/releases)

## License
Copyright (c) Virto Solutions LTD.  All rights reserved.

This software is licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense.

Unless required by the applicable law or agreed to in written form, the software
distributed under the License is provided on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
