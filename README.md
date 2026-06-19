# InCleanHome Payment Service

> Payment processing microservice — ServicePayment + PaymentMethod + MercadoPago.

Owns the `ServicePayment` and `PaymentMethod` aggregates. Handles:
- Off-platform payment method registry (Yape, Plin, bank transfer, MercadoPago).
- Manual payment registration (channels: yape, plin, bank_transfer).
- MercadoPago full integration: preference creation, payment confirmation, search by external_reference.
- Worker payout requests + balance/stats.

## Endpoints

### Payment Methods
| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/payment-methods` | List my methods |
| GET | `/api/v1/payment-methods/worker/{workerId}` | Worker's methods (where to send money) |
| POST | `/api/v1/payment-methods` | Register a method |
| PATCH | `/api/v1/payment-methods/{id}/default` | Set as default |
| DELETE | `/api/v1/payment-methods/{id}` | Delete |

### Service Payments
| Method | Path | Purpose |
|---|---|---|
| POST | `/api/v1/service-payments/booking/{id}/pay-manual` | Register manual payment (Yape/Plin/Bank) |
| GET | `/api/v1/service-payments/booking/{id}` | Get the payment for a booking |
| GET | `/api/v1/service-payments/worker/balance` | Worker stats |
| GET | `/api/v1/service-payments/worker` | Worker's payments history |
| POST | `/api/v1/service-payments/worker/request-payout` | Request payout of pending payments |

### MercadoPago
| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/mercadopago/status` | Is MP configured? |
| GET | `/api/v1/mercadopago/public-key` | Public key for SDK Bricks |
| POST | `/api/v1/mercadopago/preference` | Create preference → returns checkoutUrl |
| POST | `/api/v1/mercadopago/confirm` | Confirm by payment_id |
| POST | `/api/v1/mercadopago/confirm-by-booking` | Verify by external_reference |

## Events

### Publishes (`incleanhome.payment.events`)
- `PaymentProcessedEvent` — payment recorded (manual or MP).
- `PaymentFailedEvent` — payment attempt failed.
- `PayoutRequestedEvent` — worker requested payout.

### Consumes
- `BookingCompletedEvent` (from Booking Service) — audit only for now. The
  payment is initiated by the client via the manual or MP flow, not auto-
  generated on this event.

## HTTP Dependencies

| Target | Used for |
|---|---|
| Booking Service | Validate booking (exists, belongs to client, is completed) |
| IAM Service | Get user's email for MercadoPago checkout |

## Environment variables

| Variable | Required | Purpose |
|---|---|---|
| `JWT_SIGNING_KEY` | YES | Same key everywhere |
| `PAYMENT_DB_CONNECTION` | YES | PostgreSQL connection |
| `RABBITMQ_URL` | no | CloudAMQP URL |
| `MERCADOPAGO_ACCESS_TOKEN` | only if MP enabled | MP server-side token |
| `MERCADOPAGO_PUBLIC_KEY` | only if MP enabled | MP client-side public key |

## Run

```bash
cd ../incleanhome-platform
docker compose up --build -d payment-service
```

Direct: http://localhost:5004 · Swagger: http://localhost:5004/swagger
