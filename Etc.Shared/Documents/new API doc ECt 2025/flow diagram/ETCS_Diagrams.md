# Electronic Toll Collection System (ETCS) - Flow Diagrams

## 1. Vehicle Registration Flow Diagram

```mermaid
flowchart TD
    A[Vehicle Owner] -->|Submits registration| B[Bank's ETCS]
    B -->|Forwards details| C[Toll Plaza System]
    C -->|Validates vehicle| D[Verification]
    D -->|Success| E[ETC Registration]
    D -->|Failure| F[Rejection Notice]
    E -->|Confirmation| B
    B -->|Creates Virtual Account| G[Virtual Account]
    G -->|Shares details| C
    C -->|Confirms eligibility| A
```

## 2. Vehicle De-Registration Flow Diagram

```mermaid
flowchart TD
    A[Vehicle Owner] -->|Submits de-registration| B[Bank's ETCS]
    B -->|Forwards request| C[Toll Plaza System]
    C -->|Processes request| D[De-registration]
    D -->|Success| E[Confirmation]
    D -->|Failure| F[Error Notice]
    E -->|Update systems| B
    B -->|Notifies owner| A
```

## 3. Vehicle Top-up Flow Diagram

```mermaid
flowchart TD
    A[Vehicle Owner] -->|Initiates top-up| B[Bank's ETCS]
    B -->|Mobile App/Online/Branch| C[Payment Processing]
    C -->|Validates payment| D[Balance Update]
    D -->|Success| E[ETC Eligibility]
    D -->|Failure| F[Error Notice]
    E -->|Confirmation| A
    A -->|Balance inquiry| B
    B -->|Displays balance| A
```

## 4. Toll Plaza Pass Flow Diagram

```mermaid
flowchart TD
    A[Vehicle] -->|Enters ETC lane| B[RFID Reader]
    B -->|Detects vehicle| C[Toll Plaza System]
    C -->|Validates registration| D[Vehicle Check]
    D -->|Success| E[Authorization Request]
    D -->|Failure| F[Access Denied]
    E -->|To Bank's ETCS| G[Bank's ETCS]
    G -->|Checks balance| H[Balance Verification]
    H -->|Sufficient| I[Toll Deduction]
    H -->|Insufficient| J[Rejection]
    I -->|Authorization| C
    C -->|Opens gate| K[Vehicle Pass]
    K -->|SMS notification| A
```

## 5. Settlement Flow Diagram

```mermaid
flowchart TD
    A[End of Day] -->|Trigger| B[Bank Reconciliation]
    B -->|Collects transactions| C[Bank's ETCS]
    C -->|Verifies amounts| D[Reconciliation]
    D -->|Success| E[Fund Transfer]
    D -->|Discrepancy| F[Investigation]
    E -->|To Toll Plaza Authority| G[Settlement Bank]
    G -->|Processes transfer| H[Toll Plaza Account]
    H -->|a-Challan processing| I[Confirmation]
    I -->|To Bank's ETCS| C
```

## Diagram Key

- **Rectangles**: Processes/Steps
- **Diamonds**: Decision Points
- **Arrows**: Data/Control Flow
- **Color Coding**:
  - Blue: Bank's ETCS
  - Green: Toll Plaza System
  - Orange: Vehicle Owner Actions
  - Purple: Financial Transactions
  - Red: Error/Failure Paths

## Notes

1. All diagrams show real-time processing constraints (2-3 seconds for toll plaza pass)
2. Security measures (encryption, authentication) are implied in all API communications
3. Both success and failure paths are included where applicable
4. Timelines are indicated (T+1 for settlement)
5. System components are clearly labeled in each diagram
