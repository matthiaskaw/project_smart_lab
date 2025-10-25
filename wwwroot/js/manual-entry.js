// Manual Data Entry JavaScript

let parameters = [];
let dataRows = [];
let currentRowIndex = 0;

// Initialize the page
document.addEventListener('DOMContentLoaded', function() {
    // Set default timestamp to now
    const now = new Date();
    const localDateTime = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    document.getElementById('measurementDate').value = localDateTime;
    
    // Add initial data row
    addDataRow();
});

// Parameter management
function addParameter() {
    const parameterInputs = document.querySelectorAll('.parameter-input');
    const unitInputs = document.querySelectorAll('.unit-input');
    
    let lastInput = parameterInputs[parameterInputs.length - 1];
    let lastUnitInput = unitInputs[unitInputs.length - 1];
    
    if (lastInput.value.trim() !== '') {
        const parameterName = lastInput.value.trim();
        const unit = lastUnitInput.value.trim();
        
        if (!parameters.find(p => p.name === parameterName)) {
            parameters.push({ name: parameterName, unit: unit });
            updateParameterColumns();
            
            // Add new parameter input row
            const controlsDiv = document.getElementById('parameterControls');
            const newRow = document.createElement('div');
            newRow.className = 'row mt-2';
            newRow.innerHTML = `
                <div class="col-md-4">
                    <input type="text" class="form-control parameter-input" placeholder="Parameter name" 
                           onchange="updateParameterColumns()">
                </div>
                <div class="col-md-3">
                    <input type="text" class="form-control unit-input" placeholder="Unit (optional)">
                </div>
                <div class="col-md-3">
                    <button type="button" class="btn btn-outline-danger btn-sm" onclick="removeParameterRow(this)">
                        Remove
                    </button>
                </div>
            `;
            controlsDiv.appendChild(newRow);
        }
    }
}

function removeParameterRow(button) {
    const row = button.closest('.row');
    const parameterInput = row.querySelector('.parameter-input');
    const parameterName = parameterInput.value.trim();
    
    // Remove from parameters array
    parameters = parameters.filter(p => p.name !== parameterName);
    
    // Remove the row
    row.remove();
    
    // Update table columns
    updateParameterColumns();
}

function updateParameterColumns() {
    const table = document.getElementById('dataTable');
    const thead = table.querySelector('thead tr');
    const tbody = document.getElementById('dataTableBody');
    
    // Clear existing parameter columns (keep timestamp, notes, actions)
    while (thead.children.length > 3) {
        thead.removeChild(thead.children[1]);
    }
    
    // Add parameter columns
    parameters.forEach(param => {
        const th = document.createElement('th');
        th.textContent = param.unit ? `${param.name} (${param.unit})` : param.name;
        thead.insertBefore(th, thead.children[thead.children.length - 2]); // Before notes column
    });
    
    // Update existing rows
    Array.from(tbody.children).forEach(row => {
        updateRowStructure(row);
    });
}

function updateRowStructure(row) {
    const cells = Array.from(row.children);
    const timestampCell = cells[0];
    const notesCell = cells[cells.length - 2];
    const actionsCell = cells[cells.length - 1];
    
    // Remove parameter cells
    while (row.children.length > 3) {
        row.removeChild(row.children[1]);
    }
    
    // Add parameter cells
    parameters.forEach((param, index) => {
        const td = document.createElement('td');
        td.innerHTML = `<input type="text" class="form-control form-control-sm parameter-value" 
                                data-parameter="${param.name}" placeholder="Value">`;
        row.insertBefore(td, notesCell);
    });
}

// Data row management
function addDataRow() {
    const tbody = document.getElementById('dataTableBody');
    const row = document.createElement('tr');
    row.setAttribute('data-row-index', currentRowIndex);
    
    // Timestamp cell
    const timestampCell = document.createElement('td');
    const now = new Date();
    const timestamp = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    timestampCell.innerHTML = `<input type="datetime-local" class="form-control form-control-sm" value="${timestamp}">`;
    row.appendChild(timestampCell);
    
    // Notes cell
    const notesCell = document.createElement('td');
    notesCell.innerHTML = `<input type="text" class="form-control form-control-sm" placeholder="Notes (optional)">`;
    row.appendChild(notesCell);
    
    // Actions cell
    const actionsCell = document.createElement('td');
    actionsCell.innerHTML = `
        <button type="button" class="btn btn-outline-danger btn-sm" onclick="removeDataRow(this)">
            <i class="fas fa-trash"></i>
        </button>
    `;
    row.appendChild(actionsCell);
    
    tbody.appendChild(row);
    updateRowStructure(row);
    currentRowIndex++;
}

function removeDataRow(button) {
    const row = button.closest('tr');
    row.remove();
}

// Data collection and validation
function collectDataPoints() {
    const tbody = document.getElementById('dataTableBody');
    const rows = Array.from(tbody.children);
    const dataPoints = [];
    
    rows.forEach((row, index) => {
        const timestampInput = row.querySelector('input[type="datetime-local"]');
        const notesInput = row.querySelector('td:nth-last-child(2) input');
        const parameterInputs = row.querySelectorAll('.parameter-value');
        
        const timestamp = new Date(timestampInput.value).toISOString();
        const notes = notesInput.value.trim();
        
        parameterInputs.forEach(input => {
            const parameterName = input.getAttribute('data-parameter');
            const value = input.value.trim();
            
            if (value !== '') {
                const param = parameters.find(p => p.name === parameterName);
                dataPoints.push({
                    timestamp: timestamp,
                    parameterName: parameterName,
                    value: value,
                    unit: param ? param.unit : '',
                    notes: notes,
                    rowIndex: index
                });
            }
        });
    });
    
    return dataPoints;
}

async function validateData() {
    const dataPoints = collectDataPoints();
    
    if (dataPoints.length === 0) {
        alert('Please enter some data points first.');
        return;
    }
    
    showLoading(true);
    
    try {
        const response = await fetch('/api/ManualData/validate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(dataPoints)
        });
        
        if (response.ok) {
            const validationResult = await response.json();
            displayValidationResults(validationResult);
        } else {
            throw new Error('Validation failed');
        }
    } catch (error) {
        console.error('Validation error:', error);
        alert('Failed to validate data. Please try again.');
    } finally {
        showLoading(false);
    }
}

function displayValidationResults(result) {
    const resultsDiv = document.getElementById('validationResults');
    const summaryDiv = document.getElementById('validationSummary');
    const errorsDiv = document.getElementById('validationErrors');
    
    // Summary
    let summaryClass = result.isValid ? 'alert-success' : 'alert-warning';
    summaryDiv.innerHTML = `
        <div class="alert ${summaryClass}">
            <strong>Validation Summary:</strong><br>
            Total Rows: ${result.totalRows}<br>
            Valid Rows: ${result.validRows}<br>
            Errors: ${result.errors.length}<br>
            Warnings: ${result.warnings.length}<br>
            Parameters: ${result.detectedParameters.join(', ')}
        </div>
    `;
    
    // Errors and warnings
    let errorsHtml = '';
    if (result.errors.length > 0) {
        errorsHtml += '<h6 class="text-danger">Errors:</h6><ul class="list-group mb-3">';
        result.errors.forEach(error => {
            errorsHtml += `<li class="list-group-item list-group-item-danger">
                <strong>${error.errorType}:</strong> ${error.message}
                ${error.rowIndex !== null ? ` (Row ${error.rowIndex + 1})` : ''}
                ${error.parameterName ? ` - ${error.parameterName}` : ''}
            </li>`;
        });
        errorsHtml += '</ul>';
    }
    
    if (result.warnings.length > 0) {
        errorsHtml += '<h6 class="text-warning">Warnings:</h6><ul class="list-group">';
        result.warnings.forEach(warning => {
            errorsHtml += `<li class="list-group-item list-group-item-warning">
                <strong>${warning.errorType}:</strong> ${warning.message}
                ${warning.rowIndex !== null ? ` (Row ${warning.rowIndex + 1})` : ''}
                ${warning.parameterName ? ` - ${warning.parameterName}` : ''}
            </li>`;
        });
        errorsHtml += '</ul>';
    }
    
    errorsDiv.innerHTML = errorsHtml;
    resultsDiv.style.display = 'block';
}

async function saveDataset() {
    const name = document.getElementById('datasetName').value.trim();
    const description = document.getElementById('description').value.trim();
    const measurementDate = document.getElementById('measurementDate').value;
    
    if (!name) {
        alert('Please enter a dataset name.');
        return;
    }
    
    const dataPoints = collectDataPoints();
    
    if (dataPoints.length === 0) {
        alert('Please enter some data points first.');
        return;
    }
    
    const request = {
        name: name,
        description: description,
        measurementDate: measurementDate ? new Date(measurementDate).toISOString() : null,
        dataPoints: dataPoints,
        parameterNames: parameters.map(p => p.name),
        parameterUnits: parameters.reduce((obj, p) => {
            if (p.unit) obj[p.name] = p.unit;
            return obj;
        }, {})
    };
    
    showLoading(true);
    
    try {
        const response = await fetch('/api/ManualData/datasets', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(request)
        });
        
        if (response.ok) {
            const datasetId = await response.json();
            alert('Dataset saved successfully!');
            // Optionally redirect to data index page
            window.location.href = '/Data/DataIndex';
        } else {
            throw new Error('Failed to save dataset');
        }
    } catch (error) {
        console.error('Save error:', error);
        alert('Failed to save dataset. Please try again.');
    } finally {
        showLoading(false);
    }
}

function clearAll() {
    if (confirm('Are you sure you want to clear all data? This action cannot be undone.')) {
        // Clear form fields
        document.getElementById('datasetName').value = '';
        document.getElementById('description').value = '';
        
        // Clear parameters
        parameters = [];
        const parameterControls = document.getElementById('parameterControls');
        parameterControls.innerHTML = `
            <div class="col-md-4">
                <input type="text" class="form-control parameter-input" placeholder="Parameter name" 
                       onchange="updateParameterColumns()">
            </div>
            <div class="col-md-3">
                <input type="text" class="form-control unit-input" placeholder="Unit (optional)">
            </div>
            <div class="col-md-3">
                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="addParameter()">
                    Add Parameter
                </button>
            </div>
        `;
        
        // Clear data table
        document.getElementById('dataTableBody').innerHTML = '';
        updateParameterColumns();
        
        // Hide validation results
        document.getElementById('validationResults').style.display = 'none';
        
        // Reset row index
        currentRowIndex = 0;
        
        // Add initial row
        addDataRow();
    }
}

// File Upload functions (IDataset-based - stores metadata like measurements)
function displayFileInfo() {
    const fileInput = document.getElementById('uploadFile');
    const file = fileInput.files[0];

    if (!file) {
        document.getElementById('fileInfo').style.display = 'none';
        return;
    }

    // Display file information
    document.getElementById('fileName').textContent = file.name;
    document.getElementById('fileType').textContent = file.type || 'Unknown';
    document.getElementById('fileSize').textContent = formatFileSize(file.size);
    document.getElementById('fileInfo').style.display = 'block';

    // Auto-fill dataset name if empty
    const nameInput = document.getElementById('datasetName');
    if (!nameInput.value) {
        // Remove extension and use as dataset name
        const nameWithoutExt = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
        nameInput.value = nameWithoutExt;
    }
}

function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

async function uploadFile() {
    const fileInput = document.getElementById('uploadFile');
    const file = fileInput.files[0];
    const name = document.getElementById('datasetName').value.trim();
    const description = document.getElementById('description').value.trim();

    if (!file) {
        alert('Please select a file first.');
        return;
    }

    if (!name) {
        alert('Please enter a dataset name (required for IDataset.DatasetName).');
        return;
    }

    const formData = new FormData();
    formData.append('file', file);
    formData.append('datasetName', name);
    formData.append('description', description || '');

    // Add anti-forgery token
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    formData.append('__RequestVerificationToken', token);

    showLoading(true);

    try {
        const response = await fetch('/Data/ManualEntry?handler=Upload', {
            method: 'POST',
            body: formData
        });

        if (response.ok) {
            const result = await response.json();
            alert(`Dataset created successfully!\n\nDataset ID: ${result.datasetId}\nDataset Name: ${result.datasetName}\nFile: ${result.fileName}`);
            window.location.href = '/Data/DataIndex';
        } else {
            const errorText = await response.text();
            console.error('Upload failed:', errorText);
            throw new Error(errorText || 'Failed to upload file');
        }
    } catch (error) {
        console.error('Upload error:', error);
        alert('Failed to upload file: ' + error.message);
    } finally {
        showLoading(false);
    }
}

function clearUploadForm() {
    document.getElementById('uploadFile').value = '';
    document.getElementById('datasetName').value = '';
    document.getElementById('description').value = '';
    document.getElementById('fileInfo').style.display = 'none';
}

// Utility functions
function showLoading(show) {
    const modal = new bootstrap.Modal(document.getElementById('loadingModal'));
    if (show) {
        modal.show();
    } else {
        modal.hide();
    }
}