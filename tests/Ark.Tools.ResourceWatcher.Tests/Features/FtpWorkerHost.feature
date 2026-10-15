@ftp-workerhost
Feature: FtpWorkerHost connection pool lifetime
    As a developer watching an FTP server with FtpWorkerHost
    I want the FTP connection pool to live as long as the host
    So that each run does not reconnect and log in again

    Scenario: The FTP connection pool is reused across runs
        Given an FtpWorkerHost with a tracking FTP client pool factory
        When I run the FtpWorkerHost 3 times
        Then the FTP client pool factory should have created 1 pool
        And no FTP client pool should have been disposed
