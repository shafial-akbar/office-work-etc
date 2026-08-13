@echo off

:: Create output directory
mkdir ETCS_Diagram_Images 2>nul

:: Create individual diagram files and generate images
echo flowchart TD > diagram1.txt
echo     A[Vehicle Owner] --^>|Submits registration| B[Bank's ETCS] >> diagram1.txt
echo     B --^>|Forwards details| C[Toll Plaza System] >> diagram1.txt
echo     C --^>|Validates vehicle| D[Verification] >> diagram1.txt
echo     D --^>|Success| E[ETC Registration] >> diagram1.txt
echo     D --^>|Failure| F[Rejection Notice] >> diagram1.txt
echo     E --^>|Confirmation| B >> diagram1.txt
echo     B --^>|Creates Virtual Account| G[Virtual Account] >> diagram1.txt
echo     G --^>|Shares details| C >> diagram1.txt
echo     C --^>|Confirms eligibility| A >> diagram1.txt

echo flowchart TD > diagram2.txt
echo     A[Vehicle Owner] --^>|Submits de-registration| B[Bank's ETCS] >> diagram2.txt
echo     B --^>|Forwards request| C[Toll Plaza System] >> diagram2.txt
echo     C --^>|Processes request| D[De-registration] >> diagram2.txt
echo     D --^>|Success| E[Confirmation] >> diagram2.txt
echo     D --^>|Failure| F[Error Notice] >> diagram2.txt
echo     E --^>|Update systems| B >> diagram2.txt
echo     B --^>|Notifies owner| A >> diagram2.txt

echo flowchart TD > diagram3.txt
echo     A[Vehicle Owner] --^>|Initiates top-up| B[Bank's ETCS] >> diagram3.txt
echo     B --^>|Mobile App/Online/Branch| C[Payment Processing] >> diagram3.txt
echo     C --^>|Validates payment| D[Balance Update] >> diagram3.txt
echo     D --^>|Success| E[ETC Eligibility] >> diagram3.txt
echo     D --^>|Failure| F[Error Notice] >> diagram3.txt
echo     E --^>|Confirmation| A >> diagram3.txt
echo     A --^>|Balance inquiry| B >> diagram3.txt
echo     B --^>|Displays balance| A >> diagram3.txt

echo flowchart TD > diagram4.txt
echo     A[Vehicle] --^>|Enters ETC lane| B[RFID Reader] >> diagram4.txt
echo     B --^>|Detects vehicle| C[Toll Plaza System] >> diagram4.txt
echo     C --^>|Validates registration| D[Vehicle Check] >> diagram4.txt
echo     D --^>|Success| E[Authorization Request] >> diagram4.txt
echo     D --^>|Failure| F[Access Denied] >> diagram4.txt
echo     E --^>|To Bank's ETCS| G[Bank's ETCS] >> diagram4.txt
echo     G --^>|Checks balance| H[Balance Verification] >> diagram4.txt
echo     H --^>|Sufficient| I[Toll Deduction] >> diagram4.txt
echo     H --^>|Insufficient| J[Rejection] >> diagram4.txt
echo     I --^>|Authorization| C >> diagram4.txt
echo     C --^>|Opens gate| K[Vehicle Pass] >> diagram4.txt
echo     K --^>|SMS notification| A >> diagram4.txt

echo flowchart TD > diagram5.txt
echo     A[End of Day] --^>|Trigger| B[Bank Reconciliation] >> diagram5.txt
echo     B --^>|Collects transactions| C[Bank's ETCS] >> diagram5.txt
echo     C --^>|Verifies amounts| D[Reconciliation] >> diagram5.txt
echo     D --^>|Success| E[Fund Transfer] >> diagram5.txt
echo     D --^>|Discrepancy| F[Investigation] >> diagram5.txt
echo     E --^>|To Toll Plaza Authority| G[Settlement Bank] >> diagram5.txt
echo     G --^>|Processes transfer| H[Toll Plaza Account] >> diagram5.txt
echo     H --^>|a-Challan processing| I[Confirmation] >> diagram5.txt
echo     I --^>|To Bank's ETCS| C >> diagram5.txt

:: Generate images from diagram files
echo Generating images...
mmdc -i diagram1.txt -o ETCS_Diagram_Images\1_Vehicle_Registration.png -w 1200 -H 800 -b transparent
mmdc -i diagram2.txt -o ETCS_Diagram_Images\2_Vehicle_DeRegistration.png -w 1200 -H 800 -b transparent
mmdc -i diagram3.txt -o ETCS_Diagram_Images\3_Vehicle_Topup.png -w 1200 -H 800 -b transparent
mmdc -i diagram4.txt -o ETCS_Diagram_Images\4_Toll_Plaza_Pass.png -w 1200 -H 800 -b transparent
mmdc -i diagram5.txt -o ETCS_Diagram_Images\5_Settlement_Flow.png -w 1200 -H 800 -b transparent

:: Clean up temporary files
del diagram1.txt diagram2.txt diagram3.txt diagram4.txt diagram5.txt

echo.
echo Diagram generation complete!
echo Images saved in ETCS_Diagram_Images folder.
echo.
