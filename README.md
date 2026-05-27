FacultyFlow AI — Student Report Generator
FacultyFlow AI is a high-performance desktop application designed to streamline the academic reporting process for teachers. By leveraging modern AI neural networks, it automates the creation of personalised, high-quality student reports, allowing educators to focus more on teaching and less on administrative tasks.

Key Features
Multi-Model Intelligence: Choose between leading AI engines, including Google Gemini, OpenAI, Claude, and NVIDIA NIM, to find the perfect tone and accuracy for your reports.

Batch Processing: Efficiently generate reports for entire classes by uploading a simple CSV roster.

Customizable Frameworks: Create and save your own prompt templates to ensure reports consistently match your school’s unique pedagogical standards.

Secure Administration: Protect your configurations with a master password vault; all sensitive data is encrypted using Windows Data Protection API (DPAPI).

Seamless Integration: Export reports directly to Microsoft Word or PDF, or email them directly to parents via built-in SMTP support.

Performance Metrics: Track your saved labour hours and token usage through the built-in Analytics Dashboard.

Getting Started
Prerequisites
Windows OS (for WPF and DPAPI security features).

.NET 8.0 or higher.

A valid API key for your chosen AI provider (NVIDIA, Google, OpenAI, or Anthropic).

Installation
Clone this repository to your local machine.

Open the solution file (StudentReportGenerator.sln) in Visual Studio.

Restore NuGet packages and build the solution (Release mode recommended).

Launch the application and follow the initial branding setup.

Usage
Onboarding: Upon first launch, input your Teacher Signature and Institution Name.

Configuration: Navigate to "AI Engine Properties" and input your API keys. Note: Keys are encrypted locally and never transmitted externally except to the chosen API endpoint.

Generation:

Single Mode: Select a student, topic, and tone template, then click "Generate Report."

Batch Mode: Upload a CSV formatted as Student Name | Teacher Notes to automate class-wide workflows.

Security: Use the "Profile & Branding" tab to set a master password, ensuring your SMTP and API configurations remain secure.

Documentation
Architecture: Built using the Model-View-ViewModel (MVVM) pattern for a decoupled, testable, and maintainable codebase.

Security: Implements local machine-bound cryptography to ensure that all generated records and configuration tokens reside strictly within your workstation's local encrypted storage partitions.
