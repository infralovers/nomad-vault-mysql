package io.infralovers.nomadvaultmysql.model;

public class Customer {
    private int customerNumber;
    private String birthDate  = "";
    private String firstName  = "";
    private String lastName   = "";
    private String createDate = "";
    private String ssn        = "";
    private String ccn        = "";
    private String address    = "";
    private String salary     = "";

    public int    getCustomerNumber() { return customerNumber; }
    public void   setCustomerNumber(int customerNumber) { this.customerNumber = customerNumber; }

    public String getBirthDate()  { return birthDate; }
    public void   setBirthDate(String birthDate) { this.birthDate = birthDate; }

    public String getFirstName()  { return firstName; }
    public void   setFirstName(String firstName) { this.firstName = firstName; }

    public String getLastName()   { return lastName; }
    public void   setLastName(String lastName) { this.lastName = lastName; }

    public String getCreateDate() { return createDate; }
    public void   setCreateDate(String createDate) { this.createDate = createDate; }

    public String getSsn()        { return ssn; }
    public void   setSsn(String ssn) { this.ssn = ssn; }

    public String getCcn()        { return ccn; }
    public void   setCcn(String ccn) { this.ccn = ccn; }

    public String getAddress()    { return address; }
    public void   setAddress(String address) { this.address = address; }

    public String getSalary()     { return salary; }
    public void   setSalary(String salary) { this.salary = salary; }
}
