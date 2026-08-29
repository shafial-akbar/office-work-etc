@echo off
setlocal enabledelayedexpansion

:: Create output directory
mkdir ETCS_Diagram_Images 2>nul

:: Extract and generate images for each diagram
echo Generating Vehicle Registration Flow Diagram...
echo flowchart TD^^^r^^^n    A[Vehicle Owner] --^>|Submits registration| B[Bank's ETCS]^^^r^^^n    B --^>|Forwards details| C[Toll Plaza System]^^^r^^^n    C --^>|Validates vehicle| D[Verification]^^^r^^^n    D --^>|Success| E[ETC Registration]^^^r^^^n    D --^>|Failure| F[Rejection Notice]^^^r^^^n    E --^>|Confirmation| B^^^r^^^n    B --^>|Creates Virtual Account| G[Virtual Account]^^^r^^^n    G --^>|Shares details| C^^^r^^^n    C --^>|Confirms eligibility| A | mmdc -i - -o ETCS_Diagram_Images\1_Vehicle_Registration.png -w 1200 -H 800 -b transparent

echo Generating Vehicle De-Registration Flow Diagram...
echo flowchart TD^^^r^^^n    A[Vehicle Owner] --^>|Submits de-registration| B[Bank's ETCS]^^^r^^^n    B --^>|Forwards request| C[Toll Plaza System]^^^r^^^n    C --^>|Processes request| D[De-registration]^^^r^^^n    D --^>|Success| E[Confirmation]^^^r^^^n    D --^>|Failure| F[Error Notice]^^^r^^^n    E --^>|Update systems| B^^^r^^^n    B --^>|Notifies owner| A | mmdc -i - -o ETCS_Diagram_Images\2_Vehicle_DeRegistration.png -w 1200 -H 800 -b transparent

echo Generating Vehicle Top-up Flow Diagram...
echo flowchart TD^^^r^^^n    A[Vehicle Owner] --^>|Initiates top-up| B[Bank's ETCS]^^^r^^^n    B --^>|Mobile App/Online/Branch| C[Payment Processing]^^^r^^^n    C --^>|Validates payment| D[Balance Update]^^^r^^^n    D --^>|Success| E[ETC Eligibility]^^^r^^^n    D --^>|Failure| F[Error Notice]^^^r^^^n    E --^>|Confirmation| A^^^r^^^n    A --^>|Balance inquiry| B^^^r^^^n    B --^>|Displays balance| A | mmdc -i - -o ETCS_Diagram_Images\3_Vehicle_Topup.png -w 1200 -H 800 -b transparent

echo Generating Toll Plaza Pass Flow Diagram...
echo flowchart TD^^^r^^^n    A[Vehicle] --^>|Enters ETC lane| B[RFID Reader]^^^r^^^n    B --^>|Detects vehicle| C[Toll Plaza System]^^^r^^^n    C --^>|Validates registration| D[Vehicle Check]^^^r^^^n    D --^>|Success| E[Authorization Request]^^^r^^^n    D --^>|Failure| F[Access Denied]^^^r^^^n    E --^>|To Bank's ETCS| G[Bank's ETCS]^^^r^^^n    G --^>|Checks balance| H[Balance Verification]^^^r^^^n    H --^>|Sufficient| I[Toll Deduction]^^^r^^^n    H --^>|Insufficient| J[Rejection]^^^r^^^n    I --^>|Authorization| C^^^r^^^n    C --^>|Opens gate| K[Vehicle Pass]^^^r^^^n    K --^>|SMS notification| A | mmdc -i - -o ETCS_Diagram_Images\4_Toll_Plaza_Pass.png -w 1200 -H 800 -b transparent

echo Generating Settlement Flow Diagram...
echo flowchart TD^^^r^^^n    A[End of Day] --^>|Trigger| B[Bank Reconciliation]^^^r^^^n    B --^>|Collects transactions| C[Bank's ETCS]^^^r^^^n    C --^>|Verifies amounts| D[Reconciliation]^^^r^^^n    D --^>|Success| E[Fund Transfer]^^^r^^^n    D --^>|Discrepancy| F[Investigation]^^^r^^^n    E --^>|To Toll Plaza Authority| G[Settlement Bank]^^^r^^^n    G --^>|Processes transfer| H[Toll Plaza Account]^^^r^^^n    H --^>|a-Challan processing| I[Confirmation]^^^r^^^n    I --^>|To Bank's ETCS| C | mmdc -i - -o ETCS_Diagram_Images\5_Settlement_Flow.png -w 1200 -H 800 -b transparent

echo.
echo Diagram generation complete!
echo Images saved in ETCS_Diagram_Images folder.
echo.
