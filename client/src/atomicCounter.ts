export interface ITenant {
    tenantName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}

export class AtomicCounter {
    public static async getAuthToken(token: string): Promise<string> {
        const response = await fetch("https://atomiccounter.azurewebsites.net/.auth/login/google", {
            body: JSON.stringify({
                "id_token": token
            }),
            headers: {
                "Content-Type": "application/json"
            },
            method: "POST",
        })

        const ez = await response.json();

        return ez.authenticationToken as string;
    }

    private token: string;

    private tenants: ITenant[];

    constructor(authToken: string) {
        this.token = authToken;
    }

    public async createTenant() {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "POST"
        }).then(t => t.json() as unknown as ITenant);
    }

    public async getTenant() {
        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill", {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "GET",
        }).then(t => t.json() as unknown as ITenant);
    }

    public async increment(): Promise<void> {
        if (!this.tenants) {
           this.tenants = [await this.getTenant()];
        }

        await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/increment?key=" + this.tenants[0].writeKeys[0], {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "POST"
        });
    }

    public async count(): Promise<number> {
        if (!this.tenants) {
            this.tenants = [await this.getTenant()];
         }

        return await fetch("https://atomiccounter.azurewebsites.net/api/tenant/bill/app/bill/counter/bill/count?key=" + this.tenants[0].readKeys[0], {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            method: "GET"
        }).then(v => v.json() as unknown as number);
    }
}