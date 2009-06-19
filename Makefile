# ------------------
# Public variables
CSC ?= gmcs
MONO ?= mono
NUNIT ?= nunit-console2
GENDARME ?= gendarme
GET_VERSION ?= mget_version.sh
GEN_VERSION ?= mgen_version.sh

ifdef RELEASE
	# Note that -debug+ just generates an mdb file.
	export CSC_FLAGS ?= -checked+ -debug+ -warn:4 -nowarn:1591 -optimize+ -d:TRACE -d:CONTRACTS_PRECONDITIONS
else
	export CSC_FLAGS ?= -checked+ -debug+ -warnaserror+ -warn:4 -nowarn:1591 -d:DEBUG -d:TRACE -d:CONTRACTS_FULL
endif

INSTALL_DIR ?= /usr/local
PACKAGE_DIR ?= $(INSTALL_DIR)/lib/pkgconfig

# ------------------
# Internal variables
dummy1 := $(shell mkdir bin 2> /dev/null)
export dummy2 := $(shell if [[ "$(CSC_FLAGS)" != `cat bin/csc_flags 2> /dev/null` ]]; then echo "$(CSC_FLAGS)" > bin/csc_flags; fi)

base_version := 0.6.xxx.0										# major.minor.build.revision
version := $(shell "$(GET_VERSION)" $(base_version) build_num)	# this will increment the build number stored in build_num
export version := $(strip $(version))

# ------------------
# Primary targets		
all: lib

lib: bin/mcocoa.dll

test: bin/tests.dll
	cd bin && "$(NUNIT)" tests.dll -nologo

generate: bin/generate.exe
	cd bin && "$(MONO)" generate.exe --xml=../generate/Frameworks.xml --out=../source

update-libraries:
	cp `pkg-config --variable=Libraries mobjc` bin

# ------------------
# Binary targets 
cocoa_files := $(strip $(shell find source -name "*.cs" -print))
bin/cocoa_files: $(cocoa_files)
	@echo "$(cocoa_files)" > bin/cocoa_files
		
bin/generate.exe: generate/*.cs generate/Frameworks.xml bin/csc_flags
	$(CSC) -out:bin/generate.exe $(CSC_FLAGS) -reference:bin/mobjc.dll -target:exe generate/*.cs
		
bin/mcocoa.dll: keys bin/csc_flags bin/mobjc.dll bin/cocoa_files
	@"$(GEN_VERSION)" $(version) source/AssemblyVersion.cs
	$(CSC) -out:bin/mcocoa.dll $(CSC_FLAGS) -keyfile:keys -doc:bin/docs.xml -target:library -reference:bin/mobjc.dll @bin/cocoa_files

bin/tests.dll: bin/csc_flags tests/*.cs generate/*.cs bin/mcocoa.dll
	$(CSC) -out:bin/tests.dll $(CSC_FLAGS) -pkg:mono-nunit -target:library tests/*.cs generate/*.cs -reference:bin/mobjc.dll -reference:bin/mcocoa.dll

# ------------------
# Misc targets
keys:
	sn -k keys

docs: lib
	mmmdoc --out=docs --see-also='http://code.google.com/p/mobjc/w/list mobjc' --see-also='http://code.google.com/p/mcocoa/w/list mcocoa' bin/mcocoa.dll,bin/docs.xml

gendarme_flags := --severity all --confidence all --ignore gendarme.ignore --quiet
gendarme: bin/mobjc.dll
	@-"$(GENDARME)" $(gendarme_flags) bin/mcocoa.dll

# Note that we do not want to remove mobjc.
clean:
	-rm bin/csc_flags bin/cocoa_files
	-rm bin/mcocoa.dll bin/mcocoa.dll.mdb
	-rm bin/docs.xml

help:
	@echo "mcocoa version $(version)"
	@echo " "
	@echo "The primary targets are:"
	@echo "generate         - create the cocoa wrapper classes"
	@echo "lib              - build the library"
	@echo "test             - run the unit tests"
	@echo "update-libraries - copy the current mobjc libs into bin"
	@echo "clean            - remove the app bundles and executables from bin"
	@echo "install          - install the dll and a pkg-config file"
	@echo "uninstall        - remove the dll and the pkg-config file"
	@echo " "
	@echo "Variables include:"
	@echo "RELEASE - define to enable release builds, defaults to not defined"
	@echo "INSTALL_DIR - where to put the dll, defaults to $(INSTALL_DIR)/lib"
	@echo " "
	@echo "Here's an example:"
	@echo "sudo make RELEASE=1 install"

pc_file := $(PACKAGE_DIR)/mcocoa.pc
install: bin/mcocoa.dll
	install -d "$(PACKAGE_DIR)"
	install -d "$(INSTALL_DIR)/lib"
	install "bin/mcocoa.dll" "$(INSTALL_DIR)/lib"
ifndef RELEASE
	install "bin/mcocoa.dll.mdb" "$(INSTALL_DIR)/lib"
endif
	@echo "generating $(pc_file)"
	@echo "# Use 'cp \x60pkg-config --variable=Libraries mcocoa\x60 bin' to copy the libraries into your build directory." > $(pc_file)
	@echo "# You may also need to set PKG_CONFIG_PATH in your .bash_profile script so that it includes /usr/local/lib/pkgconfig." >> $(pc_file)
	@echo "# 'pkg-config --libs mcocoa' can be used to get the gmcs flags." >> $(pc_file)
	@echo "prefix=$(INSTALL_DIR)/lib" >> $(pc_file)
	@echo "Libraries=\x24{prefix}/mcocoa.dll\c" >> $(pc_file)
ifndef RELEASE
	@echo " \x24{prefix}/mcocoa.dll.mdb\c" >> $(pc_file)
endif
	@echo "" >> $(pc_file)
	@echo "" >> $(pc_file)
	@echo "Name: mcocoa" >> $(pc_file)
	@echo "Description: Mono <--> Cocoa Bridge" >> $(pc_file)
	@echo "Version: $(version)" >> $(pc_file)
	@echo "Libs: -r:mcocoa.dll" >> $(pc_file)

uninstall:
	-rm $(INSTALL_DIR)/lib/mcocoa.dll
	-rm $(INSTALL_DIR)/lib/mcocoa.dll.mdb
	-rm $(pc_file)

tar:
	tar --create --compress --exclude \*/.svn --exclude \*/.svn/\* --file=mcocoa-$(version).tar.gz \
		AUTHORS CHANGES CHANGE_LOG Dictionary.txt MIT.X11 Makefile README examples gendarme.ignore generate source tests

