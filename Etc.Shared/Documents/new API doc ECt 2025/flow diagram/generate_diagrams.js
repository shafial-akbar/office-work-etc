const fs = require('fs');
const { execSync } = require('child_process');

// Create output directory
if (!fs.existsSync('ETCS_Diagram_Images')) {
    fs.mkdirSync('ETCS_Diagram_Images');
}

// Diagram definitions
const diagrams = [
    {
        name: '1_Vehicle_Registration',
        content: `flowchart TD
    A[Vehicle Owner] -->|Submits registration| B[Bank's ETCS]
    B -->|Forwards details| C[Toll Plaza System]
    C -->|Validates vehicle| D[Verification]
    D -->|Success| E[ETC Registration]
    D -->|Failure| F[Rejection Notice]
    E -->|Confirmation| B
    B -->|Creates Virtual Account| G[Virtual Account]
    G -->|Shares details| C
    C -->|Confirms eligibility| A`
    },
    {
        name: '2_Vehicle_DeRegistration',
        content: `flowchart TD
    A[Vehicle Owner] -->|Submits de-registration| B[Bank's ETCS]
    B -->|Forwards request| C[Toll Plaza System]
    C -->|Processes request| D[De-registration]
    D -->|Success| E[Confirmation]
    D -->|Failure| F[Error Notice]
    E -->|Update systems| B
    B -->|Notifies owner| A`
    },
    {
        name: '3_Vehicle_Topup',
        content: `flowchart TD
    A[Vehicle Owner] -->|Initiates top-up| B[Bank's ETCS]
    B -->|Mobile App/Online/Branch| C[Payment Processing]
    C -->|Validates payment| D[Balance Update]
    D -->|Success| E[ETC Eligibility]
    D -->|Failure| F[Error Notice]
    E -->|Confirmation| A
    A -->|Balance inquiry| B
    B -->|Displays balance| A`
    },
    {
        name: '4_Toll_Plaza_Pass',
        content: `flowchart TD
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
    K -->|SMS notification| A`
    },
    {
        name: '5_Settlement_Flow',
        content: `flowchart TD
    A[End of Day] -->|Trigger| B[Bank Reconciliation]
    B -->|Collects transactions| C[Bank's ETCS]
    C -->|Verifies amounts| D[Reconciliation]
    D -->|Success| E[Fund Transfer]
    D -->|Discrepancy| F[Investigation]
    E -->|To Toll Plaza Authority| G[Settlement Bank]
    G -->|Processes transfer| H[Toll Plaza Account]
    H -->|a-Challan processing| I[Confirmation]
    I -->|To Bank's ETCS| C`
    }
];

console.log('Generating ETCS diagrams...');

// Generate each diagram
diagrams.forEach((diagram, index) => {
    const tempFile = `diagram_${index + 1}.mmd`;
    const outputFile = `ETCS_Diagram_Images/${diagram.name}.png`;

    // Write diagram to temporary file
    fs.writeFileSync(tempFile, diagram.content);

    console.log(`Generating ${diagram.name}.png...`);

    try {
        // Generate PNG image using mmdc
        execSync(`mmdc -i ${tempFile} -o ${outputFile} -w 1200 -H 800 -b transparent`, {
            stdio: 'inherit'
        });

        // Clean up temporary file
        fs.unlinkSync(tempFile);
    } catch (error) {
        console.error(`Error generating ${diagram.name}:`, error.message);
    }
});

console.log('Diagram generation complete!');
console.log('Images saved in ETCS_Diagram_Images folder.');
