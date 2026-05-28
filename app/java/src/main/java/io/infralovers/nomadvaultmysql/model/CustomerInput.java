package io.infralovers.nomadvaultmysql.model;

public class CustomerInput {
    private Integer custNo;
    private String  birthDate  = "";
    private String  firstName  = "";
    private String  lastName   = "";
    private String  createDate = "";
    private String  ssn        = "";
    private String  ccn        = "";
    private String  address    = "";
    private String  salary     = "";

    public Integer getCustNo()   { return custNo; }
    public void    setCustNo(Integer custNo) { this.custNo = custNo; }

    public String  getBirthDate()  { return birthDate; }
    public void    setBirthDate(String birthDate) { this.birthDate = birthDate; }

    public String  getFirstName()  { return firstName; }
    public void    setFirstName(String firstName) { this.firstName = firstName; }

    public String  getLastName()   { return lastName; }
    public void    setLastName(String lastName) { this.lastName = lastName; }

    public String  getCreateDate() { return createDate; }
    public void    setCreateDate(String createDate) { this.createDate = createDate; }

    public String  getSsn()        { return ssn; }
    public void    setSsn(String ssn) { this.ssn = ssn; }

    public String  getCcn()        { return ccn; }
    public void    setCcn(String ccn) { this.ccn = ccn; }

    public String  getAddress()    { return address; }
    public void    setAddress(String address) { this.address = address; }

    public String  getSalary()     { return salary; }
    public void    setSalary(String salary) { this.salary = salary; }
}
